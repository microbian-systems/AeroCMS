using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
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
    ContentSearchProjectionService? searchProjectionService = null) : IContentService
{
    /// <inheritdoc />
    public async Task<Result<ContentItem, AeroError>> LoadAsync(long siteId, long id, CancellationToken ct = default)
    {
        var item = await session.LoadAsync<ContentItem>(id, ct);
        if (item is null || item.SiteId != siteId)
            return Prelude.Fail<ContentItem, AeroError>(AeroError.CreateError($"Content item '{id}' not found."));

        await HydrateSharedFieldsAsync(item, ct);
        return item is null || item.SiteId != siteId
            ? Prelude.Fail<ContentItem, AeroError>(AeroError.CreateError($"Content item '{id}' not found."))
            : Prelude.Ok<ContentItem, AeroError>(item);
    }

    /// <inheritdoc />
    public async Task<Result<ContentItem, AeroError>> GetBySlugAsync(long siteId, string slug, CancellationToken ct = default)
    {
        var item = await session.Query<ContentItem>().FirstOrDefaultAsync(x => x.SiteId == siteId && x.Slug == slug, ct);
        if (item is not null) await HydrateSharedFieldsAsync(item, ct);
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
        if (item is not null) await HydrateSharedFieldsAsync(item, ct);
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

            if (item.Version <= 0 || item.Version != existing.Version)
                return Prelude.Fail<ContentItem, AeroError>(AeroError.ConflictError("Content item changed. Reload and try again."));
            session.UpdateExpectedVersion(existing, item.Version == 0 ? existing.Version : item.Version);
            CopyMutable(item, existing, preserveLocalizationMetadata);
            item = existing;
        }

        var type = await session.Query<ContentTypeDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == item.SiteId && x.Alias == item.ContentTypeAlias, ct);
        if (type is null)
            return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError("Content type or related content was not found."));

        item.Culture = NormalizeCulture(item.Culture);

        if (item.SourceItemId is { } sourceId && !await BelongsToSiteAsync(item.SiteId, sourceId, ct))
            return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError("Content type or related content was not found."));

        ContentTranslationGroupDocument? group = null;
        if (item.TranslationGroupId is { } groupId)
        {
            group = await session.LoadAsync<ContentTranslationGroupDocument>(groupId, ct);
            if (group is null || group.SiteId != item.SiteId ||
                !string.Equals(group.ContentTypeAlias, item.ContentTypeAlias, StringComparison.OrdinalIgnoreCase))
                return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError("Content type or related content was not found."));
        }

        if (item.ParentId is { } parentId && !await BelongsToSiteAsync(item.SiteId, parentId, ct))
            return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError("Content type or related content was not found."));

        foreach (var field in type.Fields.Where(x => x.FieldType == "reference" && !ReferenceFieldValidator.IsContentEntryReference(x)))
        {
            if (!item.Fields.TryGetValue(field.Name, out var value) || value.ValueKind is System.Text.Json.JsonValueKind.Null)
                continue;
            var multiple = field.Settings.TryGetValue("allowMultiple", out var setting) &&
                           setting.ValueKind == System.Text.Json.JsonValueKind.True;
            if (!multiple
                && value.ValueKind == System.Text.Json.JsonValueKind.String
                && string.IsNullOrWhiteSpace(value.GetString()))
            {
                continue;
            }
            var values = multiple ? value.EnumerateArray().ToArray() : [value];
            foreach (var reference in values)
            {
                if (reference.ValueKind != System.Text.Json.JsonValueKind.String ||
                    !long.TryParse(reference.GetString(), out var referenceId) ||
                    !await BelongsToSiteAsync(item.SiteId, referenceId, ct))
                    return Prelude.Fail<ContentItem, AeroError>(AeroError.NotFoundError("Content type or related content was not found."));
            }
        }

        if (item.Id == 0)
            item.Id = Snowflake.NewId();

        group ??= new ContentTranslationGroupDocument
        {
            Id = item.TranslationGroupId ?? item.Id,
            SiteId = item.SiteId,
            ContentTypeAlias = item.ContentTypeAlias,
            SourceItemId = item.SourceItemId ?? item.Id,
            SourceCulture = item.Culture
        };
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
        if (group.Version > 0)
            session.UpdateExpectedVersion(group, group.Version);
        session.Store(group);
        session.Store(item);
        if (searchProjectionService is not null)
        {
            await searchProjectionService.StageUpsertAsync(
                item,
                MapDefinition(type),
                ct);
        }
        try
        {
            await session.SaveChangesAsync(ct);
            return Prelude.Ok<ContentItem, AeroError>(item);
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
            return Prelude.Fail<bool, AeroError>(AeroError.ConflictError(
                "The source item of a translation group cannot be deleted."));
        }

        session.Delete(item);
        if (searchProjectionService is not null)
        {
            await searchProjectionService.StageDeleteAsync(siteId, id, ct);
        }
        await session.SaveChangesAsync(ct);
        return Prelude.Ok<bool, AeroError>(true);
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

    private static void CopyMutable(ContentItem inbound, ContentItem persisted, bool preserveLocalizationMetadata)
    {
        persisted.Title = inbound.Title;
        persisted.Slug = inbound.Slug;
        persisted.Culture = inbound.Culture;
        persisted.ParentId = inbound.ParentId;
        persisted.SortOrder = inbound.SortOrder;
        persisted.Fields = inbound.Fields;
        persisted.PublicationState = inbound.PublicationState;
        persisted.PublishedOn = inbound.PublishedOn;
        persisted.VersionNumber = inbound.VersionNumber;
        persisted.SchedulePublishUtc = inbound.SchedulePublishUtc;
        persisted.ScheduleUnpublishUtc = inbound.ScheduleUnpublishUtc;
        persisted.ModifiedBy = inbound.ModifiedBy;
        persisted.ModifiedOn = inbound.ModifiedOn;
        if (!preserveLocalizationMetadata)
        {
            persisted.TranslationProvenance = inbound.TranslationProvenance;
            persisted.TranslationReview = inbound.TranslationReview;
        }
    }
}
