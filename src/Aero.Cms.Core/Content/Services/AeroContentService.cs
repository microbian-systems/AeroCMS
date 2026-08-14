using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Content.Indexing;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using System.Globalization;
using System.Text.Json;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Implements <see cref="IContentService"/> with a Sable document session.
/// </summary>
public sealed class AeroContentService(
    IDocumentSession session,
    ContentSearchProjectionService? searchProjectionService = null,
    IEnumerable<IContentTranslationGroupProjectionContributor>? translationGroupProjectionContributors = null) : IContentService
{
    private readonly IReadOnlyList<IContentTranslationGroupProjectionContributor> _translationGroupProjectionContributors =
        translationGroupProjectionContributors?.ToArray() ?? [];

    /// <inheritdoc />
    public async Task<Result<ContentItem, AeroError>> LoadAsync(long siteId, long id, CancellationToken ct = default)
    {
        var item = await session.LoadAsync<ContentItem>(id, ct);
        if (item is null || item.SiteId != siteId)
            return Prelude.Fail<ContentItem, AeroError>(AeroError.CreateError($"Content item '{id}' not found."));

        item = Clone(item);
        await HydrateSharedFieldsAsync(item, ct);
        return item is null || item.SiteId != siteId
            ? Prelude.Fail<ContentItem, AeroError>(AeroError.CreateError($"Content item '{id}' not found."))
            : Prelude.Ok<ContentItem, AeroError>(item);
    }

    /// <inheritdoc />
    public async Task<Result<ContentItem, AeroError>> GetBySlugAsync(long siteId, string slug, CancellationToken ct = default)
    {
        var item = await session.Query<ContentItem>().FirstOrDefaultAsync(x => x.SiteId == siteId && x.Slug == slug, ct);
        if (item is not null)
        {
            item = Clone(item);
            await HydrateSharedFieldsAsync(item, ct);
        }
        return item is null
            ? Prelude.Fail<ContentItem, AeroError>(AeroError.CreateError($"Content item with slug '{slug}' not found."))
            : Prelude.Ok<ContentItem, AeroError>(item);
    }

    /// <inheritdoc />
    public async Task<Result<ContentItem, AeroError>> GetBySlugAndTypeAsync(
    long siteId,
    string contentTypeAlias,
    string culture,
    string slug,
    CancellationToken ct = default)
    {
        var normalizedCulture = NormalizeCulture(culture);
        var item = await session.Query<ContentItem>()
            .FirstOrDefaultAsync(x =>
                x.SiteId == siteId &&
                x.ContentTypeAlias == contentTypeAlias &&
                x.Culture == normalizedCulture &&
                x.Slug == slug,
                ct);
        if (item is not null)
        {
            item = Clone(item);
            await HydrateSharedFieldsAsync(item, ct);
        }
        return item is null
            ? Prelude.Fail<ContentItem, AeroError>(AeroError.CreateError(
                $"Content item with slug '{slug}' and culture '{normalizedCulture}' not found in type '{contentTypeAlias}'."))
            : Prelude.Ok<ContentItem, AeroError>(item);
    }

    /// <inheritdoc />
    public Task<Result<ContentItem, AeroError>> SaveAsync(ContentItem item, CancellationToken ct = default)
        => SaveCoreAsync(item, preserveLocalizationMetadata: true, ct);

    /// <summary>Persists a localization workflow mutation while preserving its server-issued metadata.</summary>
    public Task<Result<ContentItem, AeroError>> SaveLocalizationAsync(ContentItem item, CancellationToken ct = default)
        => SaveCoreAsync(item, preserveLocalizationMetadata: false, ct);

    private async Task<Result<ContentItem, AeroError>> SaveCoreAsync(ContentItem item, bool preserveLocalizationMetadata, CancellationToken ct)
    {
        ContentItem? existing = null;
        if (item.Id != 0)
        {
            existing = await session.LoadAsync<ContentItem>(item.Id, ct);
            if (existing is null || existing.SiteId != item.SiteId)
                return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError($"Content item '{item.Id}' not found."));

            // A persisted record can legitimately have storage version zero when it
            // predates version-initializing writes or was seeded outside this service.
            // Equality is the CAS invariant; requiring a positive value would reject
            // an otherwise current token without making concurrent writes safer.
            if (item.Version != existing.Version)
                return Prelude.Fail<ContentItem, AeroError>(AeroError.ConflictError("Content item changed. Reload and try again."));
            // UpdateExpectedVersion queues the tracked document.  Build a detached
            // candidate instead of overlaying a hydrated caller copy on it.
            item = CopyMutable(item, existing, preserveLocalizationMetadata);
            session.UpdateExpectedVersion(item, item.Version);
        }

        var type = await session.Query<ContentTypeDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == item.SiteId && x.Alias == item.ContentTypeAlias, ct);
        if (type is null)
            return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError("Content type or related content was not found."));

        item.Culture = NormalizeCulture(item.Culture);

        if (item.SourceItemId is { } sourceId && !await BelongsToSiteAsync(item.SiteId, sourceId, ct))
            return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError("Content type or related content was not found."));

        ContentTranslationGroupDocument? group = null;
        var isNewGroup = false;
        if (item.TranslationGroupId is { } groupId)
        {
            group = await session.LoadAsync<ContentTranslationGroupDocument>(groupId, ct);
            if (group is not null && (group.SiteId != item.SiteId ||
                !string.Equals(group.ContentTypeAlias, item.ContentTypeAlias, StringComparison.OrdinalIgnoreCase))
            )
                return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError("Content type or related content was not found."));
            if (group is not null)
                group = Clone(group);
            else if (preserveLocalizationMetadata || item.SourceItemId is not { } sourceItemId)
                return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError("Content type or related content was not found."));
            else
            {
                var source = await session.LoadAsync<ContentItem>(sourceItemId, ct);
                if (source is null || source.SiteId != item.SiteId
                    || !string.Equals(source.ContentTypeAlias, item.ContentTypeAlias, StringComparison.OrdinalIgnoreCase))
                {
                    return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError("Content type or related content was not found."));
                }

                group = new ContentTranslationGroupDocument
                {
                    Id = groupId,
                    SiteId = item.SiteId,
                    ContentTypeAlias = item.ContentTypeAlias,
                    SourceItemId = source.Id,
                    SourceCulture = source.Culture
                };
                isNewGroup = true;
            }
        }

        if (existing is not null && preserveLocalizationMetadata && HasTranslationRelevantChange(item, existing, type.Fields))
        {
            InvalidateAiTranslationRevision(item);
            if (group?.SourceItemId == existing.Id)
                await InvalidateAiTranslationVariantsForChangedSourceAsync(item.SiteId, existing.Id, ct);
        }

        if (item.ParentId is { } parentId && !await BelongsToSiteAsync(item.SiteId, parentId, ct))
            return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError("Content type or related content was not found."));

        var referenceValidation = await ValidateReferenceFieldsAsync(item, type.Fields, ct);
        if (referenceValidation is Result<NoneType, AeroError>.Failure referenceFailure)
            return Prelude.Fail<ContentItem, AeroError>(referenceFailure.Error);

        if (item.Id == 0)
            item.Id = Snowflake.NewId();

        if (group is null)
        {
            group = new ContentTranslationGroupDocument
            {
                Id = item.TranslationGroupId ?? item.Id,
                SiteId = item.SiteId,
                ContentTypeAlias = item.ContentTypeAlias,
                SourceItemId = item.SourceItemId ?? item.Id,
                SourceCulture = item.Culture
            };
            isNewGroup = true;
        }
        item.TranslationGroupId = group.Id;

        // A shared field is authoritative only in the group document. Split it before
        // persistence so callers cannot accidentally leave a second durable copy on an item.
        var sharedNames = type.Fields
            .Where(field => field.LocalizationMode == ContentFieldLocalizationMode.Shared)
            .Select(field => field.Name)
            .ToHashSet(StringComparer.Ordinal);
        var changedShared = false;
        foreach (var name in sharedNames)
        {
            if (item.Fields.Remove(name, out var value)
                && (!preserveLocalizationMetadata || existing is null)
                && (!group.SharedFields.TryGetValue(name, out var existingShared) || !JsonElement.DeepEquals(existingShared, value)))
            {
                group.SharedFields[name] = value;
                changedShared = true;
            }
        }
        if (changedShared)
        {
            group.Revision++;
            group.ModifiedOn = DateTimeOffset.UtcNow;
        }
        if (changedShared && group.Version > 0)
            session.UpdateExpectedVersion(group, group.Version);
        if (isNewGroup)
            session.Store(group);
        if (isNewGroup)
        {
            var projectionResult = await StageTranslationGroupProjectionAsync(
                group,
                ContentTranslationGroupProjectionChange.Upsert,
                ct);
            if (projectionResult is Result<NoneType, AeroError>.Failure projectionFailure)
            {
                session.ClearChanges();
                return Prelude.Fail<ContentItem, AeroError>(projectionFailure.Error);
            }
        }
        if (existing is null)
            session.Store(item);
        if (searchProjectionService is not null)
        {
            await searchProjectionService.StageUpsertAsync(
                item,
                MapDefinition(type),
                group.SharedFields,
                ct);
        }
        try
        {
            await session.SaveChangesAsync(ct);
            // Never leak a session-tracked instance across the service boundary. A
            // later save in the same scope may advance the tracked storage version,
            // which would silently turn a caller's stale CAS token into a current one.
            var result = Clone(item);
            if (existing is not null)
                result.Version = checked(existing.Version + 1);
            foreach (var (name, value) in group.SharedFields)
                result.Fields[name] = value.Clone();
            return Prelude.Ok<ContentItem, AeroError>(result);
        }
        catch (ConcurrencyException)
        {
            session.ClearChanges();
            return Prelude.Fail<ContentItem, AeroError>(AeroError.ConflictError("Content item or translation group changed. Reload and try again."));
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(long siteId, long id, CancellationToken ct = default)
        => await session.LoadAsync<ContentItem>(id, ct) is { } item && item.SiteId == siteId;

    /// <inheritdoc />
    public async Task<Result<bool, AeroError>> DeleteAsync(long siteId, long id, CancellationToken ct = default)
    {
        var item = await session.LoadAsync<ContentItem>(id, ct);
        if (item is null || item.SiteId != siteId)
            return Prelude.Fail<bool, AeroError>(AeroError.NotFoundError($"Content item '{id}' not found."));

        var hasChildren = await session.Query<ContentItem>()
            .Where(candidate => candidate.SiteId == siteId && candidate.ParentId == id)
            .AnyAsync(ct);
        if (hasChildren)
        {
            return Prelude.Fail<bool, AeroError>(
                AeroError.ConflictError(
                    "This content item has children. Move or delete its children before deleting it."));
        }

        var group = item.TranslationGroupId is { } groupId
            ? await session.LoadAsync<ContentTranslationGroupDocument>(groupId, ct)
            : null;
        if (group?.SourceItemId == id)
        {
            var hasTranslations = await session.Query<ContentItem>()
                .Where(candidate => candidate.SiteId == siteId
                    && candidate.TranslationGroupId == group.Id
                    && candidate.Id != id)
                .AnyAsync(ct);
            if (hasTranslations)
            {
                return Prelude.Fail<bool, AeroError>(AeroError.ConflictError(
                    "The source item of a translation group cannot be deleted while translations exist."));
            }

            var projectionResult = await StageTranslationGroupProjectionAsync(
                group,
                ContentTranslationGroupProjectionChange.Delete,
                ct);
            if (projectionResult is Result<NoneType, AeroError>.Failure projectionFailure)
            {
                session.ClearChanges();
                return Prelude.Fail<bool, AeroError>(projectionFailure.Error);
            }
            session.Delete(group);
        }

        session.Delete(item);
        if (searchProjectionService is not null)
        {
            await searchProjectionService.StageDeleteAsync(siteId, id, ct);
        }
        try
        {
            await session.SaveChangesAsync(ct);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (ConcurrencyException)
        {
            session.ClearChanges();
            return Prelude.Fail<bool, AeroError>(AeroError.ConflictError(
                "The content item or one of its relationships changed. Reload and try again."));
        }
    }

    /// <summary>
    /// Validates that ordinary content-item reference fields resolve within the item's site.
    /// Content-entry references are structurally validated by the regular field validator and
    /// intentionally are not interpreted as content item identifiers.
    /// </summary>
    public async Task<Result<NoneType, AeroError>> ValidateReferenceFieldsAsync(
        ContentItem item,
        IReadOnlyList<ContentFieldDefinition> fields,
        CancellationToken ct = default)
    {
        foreach (var field in fields.Where(x =>
                     x.FieldType == ContentFieldTypes.Reference
                     && !ReferenceFieldValidator.IsContentEntryReference(x)
                     && !ReferenceFieldValidator.IsCmsDocumentReference(x)))
        {
            if (!item.Fields.TryGetValue(field.Name, out var value) || value.ValueKind is System.Text.Json.JsonValueKind.Null)
                continue;
            var multiple = field.Settings.TryGetValue("allowMultiple", out var setting) &&
                           setting.ValueKind == System.Text.Json.JsonValueKind.True;
            if (!multiple && value.ValueKind == System.Text.Json.JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()))
                continue;

            var values = multiple ? value.EnumerateArray().ToArray() : [value];
            foreach (var reference in values)
            {
                if (reference.ValueKind != System.Text.Json.JsonValueKind.String ||
                    !long.TryParse(reference.GetString(), out var referenceId) ||
                    !await BelongsToSiteAsync(item.SiteId, referenceId, ct))
                    return Prelude.Fail<NoneType, AeroError>(AeroError.NotFoundError("Content type or related content was not found."));
            }
        }

        return Prelude.Ok<NoneType, AeroError>(default);
    }

    private static string NormalizeCulture(string? culture) =>
        string.IsNullOrWhiteSpace(culture)
            ? "en-US"
            : CultureInfo.GetCultureInfo(culture.Trim()).Name;

    private async Task<bool> BelongsToSiteAsync(long siteId, long id, CancellationToken ct)
        => await session.LoadAsync<ContentItem>(id, ct) is { } item && item.SiteId == siteId;

    private async Task HydrateSharedFieldsAsync(ContentItem item, CancellationToken ct)
    {
        if (item.TranslationGroupId is not { } groupId) return;
        var group = await session.LoadAsync<ContentTranslationGroupDocument>(groupId, ct);
        if (group is null || group.SiteId != item.SiteId) return;
        foreach (var (name, value) in group.SharedFields)
            item.Fields[name] = value.Clone();
    }

    private async Task<Result<NoneType, AeroError>> StageTranslationGroupProjectionAsync(
        ContentTranslationGroupDocument group,
        ContentTranslationGroupProjectionChange change,
        CancellationToken cancellationToken)
    {
        if (_translationGroupProjectionContributors.Count == 0)
            return Prelude.Ok<NoneType, AeroError>(default);

        var context = new ContentTranslationGroupProjectionContext(
            group.SiteId,
            group.ContentTypeAlias,
            group.Id,
            group.SourceItemId,
            group.Revision,
            group.SharedFields.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Clone(),
                StringComparer.Ordinal),
            change);

        foreach (var contributor in _translationGroupProjectionContributors)
        {
            var result = await contributor.StageAsync(session, context, cancellationToken);
            if (result is Result<NoneType, AeroError>.Failure)
                return result;
        }

        return Prelude.Ok<NoneType, AeroError>(default);
    }

    private async Task InvalidateAiTranslationVariantsForChangedSourceAsync(long siteId, long sourceItemId, CancellationToken ct)
    {
        var variants = await session.Query<ContentItem>()
            .Where(candidate => candidate.SiteId == siteId
                && candidate.SourceItemId == sourceItemId)
            .ToListAsync(ct);

        foreach (var variant in variants.Where(candidate => candidate.TranslationProvenance?.Origin == ContentTranslationOrigin.AiAssisted))
        {
            if (variant.Id == sourceItemId) continue;
            session.UpdateExpectedVersion(variant, variant.Version);
            InvalidateAiTranslationRevision(variant);
        }
    }

    private static void InvalidateAiTranslationRevision(ContentItem item)
    {
        if (item.TranslationProvenance?.Origin != ContentTranslationOrigin.AiAssisted) return;
        item.TranslationReview = ContentTranslationReview.Pending("The source or translation content changed.");
        item.PublicationState = ContentPublicationState.Draft;
        item.PublishedOn = null;
        item.SchedulePublishUtc = null;
    }

    private static bool HasTranslationRelevantChange(
        ContentItem candidate,
        ContentItem persisted,
        IReadOnlyList<ContentFieldDefinition> fields) =>
        !string.Equals(candidate.Title, persisted.Title, StringComparison.Ordinal)
        || !string.Equals(candidate.Slug, persisted.Slug, StringComparison.Ordinal)
        || !FieldsEqual(
            ExcludingShared(candidate.Fields, fields),
            ExcludingShared(persisted.Fields, fields));

    private static IReadOnlyDictionary<string, JsonElement> ExcludingShared(
        IReadOnlyDictionary<string, JsonElement> fields,
        IReadOnlyList<ContentFieldDefinition> definitions)
    {
        var shared = definitions
            .Where(field => field.LocalizationMode == ContentFieldLocalizationMode.Shared)
            .Select(field => field.Name)
            .ToHashSet(StringComparer.Ordinal);
        return fields
            .Where(pair => !shared.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static bool FieldsEqual(
        IReadOnlyDictionary<string, JsonElement> left,
        IReadOnlyDictionary<string, JsonElement> right) =>
        left.Count == right.Count
        && left.All(pair => right.TryGetValue(pair.Key, out var value) && JsonElement.DeepEquals(pair.Value, value));

    private static ContentTypeDefinition MapDefinition(ContentTypeDocument document)
        => new()
        {
            Id = document.Id,
            SiteId = document.SiteId,
            Alias = document.Alias,
            Name = document.Name,
            IncludeInSearch = document.IncludeInSearch,
            IncludeInPublicAi = document.IncludeInPublicAi,
            Localization = document.Localization,
            Fields = document.Fields
    };

    private static ContentItem CopyMutable(ContentItem inbound, ContentItem persisted, bool preserveLocalizationMetadata)
    {
        var copy = Clone(persisted);
        copy.Title = inbound.Title;
        copy.Slug = inbound.Slug;
        copy.Culture = inbound.Culture;
        copy.ParentId = inbound.ParentId;
        copy.SortOrder = inbound.SortOrder;
        copy.Fields = inbound.Fields.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), inbound.Fields.Comparer);
        copy.PublicationState = inbound.PublicationState;
        copy.PublishedOn = inbound.PublishedOn;
        copy.VersionNumber = inbound.VersionNumber;
        copy.SchedulePublishUtc = inbound.SchedulePublishUtc;
        copy.ScheduleUnpublishUtc = inbound.ScheduleUnpublishUtc;
        copy.ModifiedBy = inbound.ModifiedBy;
        copy.ModifiedOn = inbound.ModifiedOn;
        if (!preserveLocalizationMetadata)
        {
            copy.TranslationProvenance = inbound.TranslationProvenance;
            copy.TranslationReview = inbound.TranslationReview;
        }
        return copy;
    }

    private static ContentItem Clone(ContentItem source) => new()
    {
        Id = source.Id, Version = source.Version, SiteId = source.SiteId,
        ContentTypeAlias = source.ContentTypeAlias, Slug = source.Slug, Title = source.Title,
        TranslationGroupId = source.TranslationGroupId, Culture = source.Culture, SourceItemId = source.SourceItemId,
        ParentId = source.ParentId, SortOrder = source.SortOrder,
        Fields = source.Fields.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), source.Fields.Comparer),
        PublicationState = source.PublicationState, PublishedOn = source.PublishedOn, VersionNumber = source.VersionNumber,
        SchedulePublishUtc = source.SchedulePublishUtc, ScheduleUnpublishUtc = source.ScheduleUnpublishUtc,
        CreatedOn = source.CreatedOn, ModifiedOn = source.ModifiedOn, CreatedBy = source.CreatedBy, ModifiedBy = source.ModifiedBy,
        TranslationProvenance = source.TranslationProvenance,
        TranslationReview = new(source.TranslationReview.Status, source.TranslationReview.ReviewedOn,
            source.TranslationReview.ReviewedBy, source.TranslationReview.Notes,
            source.TranslationReview.ReviewedSourceItemId, source.TranslationReview.ReviewedSourceVersionNumber,
            source.TranslationReview.ReviewedTargetVersionNumber)
    };

    private static ContentTranslationGroupDocument Clone(ContentTranslationGroupDocument source) => new()
    {
        Id = source.Id, Version = source.Version, SiteId = source.SiteId,
        ContentTypeAlias = source.ContentTypeAlias, SourceItemId = source.SourceItemId,
        SourceCulture = source.SourceCulture, Revision = source.Revision,
        SharedFields = source.SharedFields.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), source.SharedFields.Comparer),
        CreatedOn = source.CreatedOn, ModifiedOn = source.ModifiedOn, CreatedBy = source.CreatedBy, ModifiedBy = source.ModifiedBy
    };
}
