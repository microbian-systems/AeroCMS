using System.Globalization;
using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Abstractions.Enums;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Services;

/// <summary>Executes site-scoped culture forks and bounded AI translation application.</summary>
public sealed class ContentLocalizationHandler(
    IDocumentSession session,
    IContentService contentService,
    AeroContentService writableContentService,
    IContentTypeService contentTypeService,
    ContentValidationService validation) : IContentLocalizationHandler
{
    public async Task<Result<ContentLocalizationOperationResult, AeroError>> ForkAsync(
        ContentLocalizationContext context,
        ContentCultureForkCommand command,
        CancellationToken cancellationToken = default)
    {
        var targetCulture = ValidateContextCulture(context, command.TargetCulture);
        if (targetCulture is null || string.IsNullOrWhiteSpace(command.TargetSlug))
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(
                AeroError.ValidationError(["A supported target culture and nonblank slug are required."]));

        var source = await contentService.LoadAsync(context.SiteId, command.SourceItemId, cancellationToken);
        if (source is not Result<ContentItem, AeroError>.Ok sourceOk)
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.NotFoundError("The source content item was not found."));
        if (!RequiredMatch(command.ExpectedSourceStorageVersion, sourceOk.Value.Version))
            return Conflict();

        var groupId = sourceOk.Value.TranslationGroupId ?? sourceOk.Value.Id;
        var group = await session.LoadAsync<ContentTranslationGroupDocument>(groupId, cancellationToken);
        if (group is null)
        {
            if (command.ExpectedGroupStorageVersion is not null)
                return Conflict();
        }
        else if (group.SiteId != context.SiteId
                 || !string.Equals(group.ContentTypeAlias, sourceOk.Value.ContentTypeAlias, StringComparison.OrdinalIgnoreCase)
                 || group.SourceItemId != sourceOk.Value.Id
                 || !RequiredMatch(command.ExpectedGroupStorageVersion, group.Version))
            return Conflict();

        var type = await contentTypeService.GetByAliasAsync(context.SiteId, sourceOk.Value.ContentTypeAlias, cancellationToken);
        if (type is not Result<ContentTypeDefinition, AeroError>.Ok typeOk)
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.NotFoundError("The source content type was not found."));

        var existing = await session.Query<ContentItem>()
            .FirstOrDefaultAsync(item => item.SiteId == context.SiteId
                && item.ContentTypeAlias == sourceOk.Value.ContentTypeAlias
                && item.TranslationGroupId == groupId
                && item.Culture == targetCulture, cancellationToken);
        if (existing is not null && !command.OverwriteExisting)
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(
                AeroError.ConflictError("A variant already exists for the requested culture."));
        if (existing is not null && !RequiredMatch(command.ExpectedTargetStorageVersion, existing.Version))
            return Conflict();

        var fork = existing ?? new ContentItem
        {
            SiteId = context.SiteId,
            ContentTypeAlias = sourceOk.Value.ContentTypeAlias,
            TranslationGroupId = groupId,
            SourceItemId = sourceOk.Value.Id,
            Culture = targetCulture,
            PublicationState = ContentPublicationState.Draft
        };
        fork.Slug = command.TargetSlug.Trim().Trim('/');
        fork.Title = sourceOk.Value.Title;
        fork.Fields = CopyForkableFields(sourceOk.Value.Fields, typeOk.Value.Fields);
        fork.TranslationProvenance = new ContentTranslationProvenance(
            ContentTranslationOrigin.Fork, sourceOk.Value.Culture, sourceOk.Value.VersionNumber, DateTimeOffset.UtcNow);
        fork.TranslationReview = new ContentTranslationReview();
        fork.VersionNumber = (existing?.VersionNumber ?? 0) + 1;

        var draftValidation = await validation.ValidateAsync(fork, ContentValidationMode.Draft, cancellationToken);
        if (draftValidation is Result<ContentItem, AeroError>.Failure validationFailure)
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(validationFailure.Error);

        // A missing target is insert-only. Do not retain a transient identity assigned by
        // validation or session tracking; an overwrite always keeps the existing identity.
        if (existing is null)
            fork.Id = 0;

        var sourceFence = session.FenceExpectedVersion<ContentItem>(sourceOk.Value.Id, sourceOk.Value.Version);
        var groupFence = group is null
            ? null
            : session.FenceExpectedVersion<ContentTranslationGroupDocument>(group.Id, group.Version);
        Result<ContentItem, AeroError> saved;
        try
        {
            saved = await contentService.SaveLocalizationAsync(fork, cancellationToken);
        }
        catch (ConcurrencyException)
        {
            session.ClearChanges();
            return Conflict();
        }
        return saved is Result<ContentItem, AeroError>.Ok savedOk
            ? ToResult(savedOk.Value, sourceFence, groupFence, group?.Version ?? 0, group?.Revision ?? 0)
            : saved is Result<ContentItem, AeroError>.Failure savedFailure
                ? Prelude.Fail<ContentLocalizationOperationResult, AeroError>(savedFailure.Error)
                : Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.ConflictError("The culture variant could not be saved."));
    }

    public async Task<Result<ContentLocalizationOperationResult, AeroError>> ApplyAiTranslationAsync(
        ContentLocalizationContext context,
        ApplyContentAiTranslationCommand command,
        CancellationToken cancellationToken = default)
    {
        var sourceCulture = ValidateContextCulture(context, command.SourceCulture);
        var targetCulture = ValidateContextCulture(context, command.TargetCulture);
        if (sourceCulture is null || targetCulture is null || sourceCulture == targetCulture)
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.ValidationError(["Supported, distinct source and target cultures are required."]));

        var source = await contentService.LoadAsync(context.SiteId, command.SourceItemId, cancellationToken);
        var target = await contentService.LoadAsync(context.SiteId, command.TargetItemId, cancellationToken);
        if (source is not Result<ContentItem, AeroError>.Ok sourceOk || target is not Result<ContentItem, AeroError>.Ok targetOk
            || sourceOk.Value.VersionNumber != command.SourceVersionNumber
            || targetOk.Value.VersionNumber != command.ExpectedTargetVersionNumber
            || !string.Equals(sourceOk.Value.Culture, sourceCulture, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(targetOk.Value.Culture, targetCulture, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(sourceOk.Value.ContentTypeAlias, targetOk.Value.ContentTypeAlias, StringComparison.OrdinalIgnoreCase)
            || sourceOk.Value.TranslationGroupId != targetOk.Value.TranslationGroupId)
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.ConflictError("The translation source or target revision is stale or invalid."));
        if (!RequiredMatch(command.ExpectedSourceStorageVersion, sourceOk.Value.Version)
            || !RequiredMatch(command.ExpectedTargetStorageVersion, targetOk.Value.Version)) return Conflict();
        var group = await LoadExactGroupAsync(context, sourceOk.Value, targetOk.Value, cancellationToken);
        if (group is null || !RequiredMatch(command.ExpectedGroupStorageVersion, group.Version)) return Conflict();

        var type = await contentTypeService.GetByAliasAsync(context.SiteId, targetOk.Value.ContentTypeAlias, cancellationToken);
        if (type is not Result<ContentTypeDefinition, AeroError>.Ok typeOk)
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.NotFoundError("The target content type was not found."));

        var allowed = typeOk.Value.Fields
            .Where(field => field.LocalizationMode == ContentFieldLocalizationMode.Localized && field.FieldType != ContentFieldTypes.Reference)
            .Select(field => field.Name)
            .ToHashSet(StringComparer.Ordinal);
        if (command.TranslatedFields.Keys.Any(key => !allowed.Contains(key)))
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.ValidationError(["AI translation may update only localized or copy-on-fork fields."]));

        foreach (var (name, value) in command.TranslatedFields)
            targetOk.Value.Fields[name] = value.Clone();
        targetOk.Value.TranslationProvenance = new ContentTranslationProvenance(
            ContentTranslationOrigin.AiAssisted, sourceCulture, sourceOk.Value.VersionNumber,
            DateTimeOffset.UtcNow, command.ProviderId, command.Model);
        targetOk.Value.SourceItemId = sourceOk.Value.Id;
        targetOk.Value.TranslationReview = typeOk.Value.Localization.AiTranslationReviewPolicy
            == ContentAiTranslationReviewPolicy.RequireHumanReview
            ? ContentTranslationReview.Pending()
            : new ContentTranslationReview();
        // An AI application creates a new editorial revision. It must never leave an
        // already-published variant visible while review metadata has changed.
        targetOk.Value.PublicationState = ContentPublicationState.Draft;
        targetOk.Value.PublishedOn = null;
        targetOk.Value.SchedulePublishUtc = null;
        targetOk.Value.VersionNumber++;

        var draftValidation = await validation.ValidateAsync(targetOk.Value, ContentValidationMode.Draft, cancellationToken);
        if (draftValidation is Result<ContentItem, AeroError>.Failure validationFailure)
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(validationFailure.Error);

        var sourceFence = session.FenceExpectedVersion<ContentItem>(sourceOk.Value.Id, sourceOk.Value.Version);
        var groupFence = session.FenceExpectedVersion<ContentTranslationGroupDocument>(group.Id, group.Version);
        Result<ContentItem, AeroError> saved;
        try
        {
            saved = await contentService.SaveLocalizationAsync(targetOk.Value, cancellationToken);
        }
        catch (ConcurrencyException)
        {
            session.ClearChanges();
            return Conflict();
        }
        return saved is Result<ContentItem, AeroError>.Ok savedOk
            ? ToResult(savedOk.Value, sourceFence, groupFence, group.Version, group.Revision)
            : Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.ConflictError("The AI translation could not be saved."));
    }

    public async Task<Result<ContentLocalizationOperationResult, AeroError>> ReviewAsync(
        ContentLocalizationContext context,
        ReviewContentTranslationCommand command,
        CancellationToken cancellationToken = default)
    {
        var source = await contentService.LoadAsync(context.SiteId, command.SourceItemId, cancellationToken);
        var target = await contentService.LoadAsync(context.SiteId, command.TargetItemId, cancellationToken);
        if (source is not Result<ContentItem, AeroError>.Ok sourceOk || target is not Result<ContentItem, AeroError>.Ok targetOk
            || sourceOk.Value.VersionNumber != command.SourceVersionNumber
            || targetOk.Value.VersionNumber != command.TargetVersionNumber
            || targetOk.Value.TranslationProvenance?.Origin != ContentTranslationOrigin.AiAssisted
            || targetOk.Value.TranslationProvenance.SourceVersionNumber != sourceOk.Value.VersionNumber
            || targetOk.Value.SourceItemId != sourceOk.Value.Id)
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.ConflictError("The translation review is stale or does not match an AI-assisted variant."));
        if (!RequiredMatch(command.ExpectedSourceStorageVersion, sourceOk.Value.Version)
            || !RequiredMatch(command.ExpectedTargetStorageVersion, targetOk.Value.Version)) return Conflict();
        var group = await LoadExactGroupAsync(context, sourceOk.Value, targetOk.Value, cancellationToken);
        if (group is null || !RequiredMatch(command.ExpectedGroupStorageVersion, group.Version)) return Conflict();

        targetOk.Value.TranslationReview = command.Approved
            ? ContentTranslationReview.Approve(sourceOk.Value.Id, sourceOk.Value.VersionNumber, targetOk.Value.VersionNumber, DateTimeOffset.UtcNow, notes: command.Notes)
            : ContentTranslationReview.Reject(sourceOk.Value.Id, sourceOk.Value.VersionNumber, targetOk.Value.VersionNumber, DateTimeOffset.UtcNow, notes: command.Notes);
        var sourceFence = session.FenceExpectedVersion<ContentItem>(sourceOk.Value.Id, sourceOk.Value.Version);
        var groupFence = session.FenceExpectedVersion<ContentTranslationGroupDocument>(group.Id, group.Version);
        Result<ContentItem, AeroError> saved;
        try
        {
            saved = await contentService.SaveLocalizationAsync(targetOk.Value, cancellationToken);
        }
        catch (ConcurrencyException)
        {
            session.ClearChanges();
            return Conflict();
        }
        return saved is Result<ContentItem, AeroError>.Ok savedOk
            ? ToResult(savedOk.Value, sourceFence, groupFence, group.Version, group.Revision)
            : Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.ConflictError("The translation review could not be saved."));
    }

    public async Task<Result<ContentLocalizationOperationResult, AeroError>> UpdateSharedFieldsAsync(
        ContentLocalizationContext context,
        UpdateContentTranslationSharedFieldsCommand command,
        CancellationToken cancellationToken = default)
    {
        var group = await session.LoadAsync<ContentTranslationGroupDocument>(command.TranslationGroupId, cancellationToken);
        if (group is null || group.SiteId != context.SiteId || group.Version != command.ExpectedGroupStorageVersion || group.Revision != command.ExpectedGroupRevision)
            return Conflict();
        var source = await session.LoadAsync<ContentItem>(group.SourceItemId, cancellationToken);
        if (source is null || source.SiteId != context.SiteId
            || !string.Equals(source.ContentTypeAlias, group.ContentTypeAlias, StringComparison.OrdinalIgnoreCase)
            || source.TranslationGroupId != group.Id)
        {
            return Conflict();
        }
        var type = await contentTypeService.GetByAliasAsync(context.SiteId, group.ContentTypeAlias, cancellationToken);
        if (type is not Result<ContentTypeDefinition, AeroError>.Ok typeOk
            || command.SharedFields.Keys.Any(name => !typeOk.Value.Fields.Any(field => field.Name == name && field.LocalizationMode == ContentFieldLocalizationMode.Shared)))
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.ValidationError(["Only declared shared fields may be changed through the translation group."]));

        // Treat a shared update as a patch. A caller cannot accidentally erase an
        // omitted required value, and every persisted variant is validated using the
        // same field/reference rules as a normal content save before the group changes.
        var candidateShared = group.SharedFields
            .ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
        foreach (var (name, value) in command.SharedFields)
            candidateShared[name] = value.Clone();

        var requiredSharedMissing = typeOk.Value.Fields
            .Where(field => field.LocalizationMode == ContentFieldLocalizationMode.Shared && field.Required)
            .Any(field => !candidateShared.TryGetValue(field.Name, out var value) || value.ValueKind == JsonValueKind.Null);
        if (requiredSharedMissing)
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.ValidationError(["Required shared fields cannot be removed."]));

        var variants = await session.Query<ContentItem>()
            .Where(item => item.SiteId == context.SiteId && item.TranslationGroupId == group.Id)
            .ToListAsync(cancellationToken);
        foreach (var variant in variants)
        {
            var candidate = Clone(variant);
            foreach (var (name, value) in candidateShared)
                candidate.Fields[name] = value.Clone();
            var validationResult = await validation.ValidateAsync(candidate, ContentValidationMode.Publish, cancellationToken);
            if (validationResult is Result<ContentItem, AeroError>.Failure validationFailure)
                return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(validationFailure.Error);
            var referenceResult = await writableContentService.ValidateReferenceFieldsAsync(candidate, typeOk.Value.Fields, cancellationToken);
            if (referenceResult is Result<NoneType, AeroError>.Failure referenceFailure)
                return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(referenceFailure.Error);
        }

        var sourceFence = session.FenceExpectedVersion<ContentItem>(source.Id, source.Version);
        session.UpdateExpectedVersion(group, command.ExpectedGroupStorageVersion);
        group.SharedFields = candidateShared;
        group.Revision++;
        group.ModifiedOn = DateTimeOffset.UtcNow;
        session.Store(new ContentTranslationProjectionWorkDocument
        {
            Id = Snowflake.NewId(),
            WorkKey = $"{group.Id}:{group.Version + 1}:{group.Revision}",
            SiteId = group.SiteId,
            TranslationGroupId = group.Id,
            GroupStorageVersion = group.Version + 1,
            GroupRevision = group.Revision
        });
        try
        {
            await session.SaveChangesAsync(cancellationToken);
            if (sourceFence.Status != VersionFenceStatus.Committed || sourceFence.CommittedVersion is not { } sourceVersion)
                return Conflict();
            return Prelude.Ok<ContentLocalizationOperationResult, AeroError>(new(
                group.SourceItemId,
                group.Id,
                group.SourceCulture,
                ContentTranslationReviewStatus.NotRequired,
                sourceVersion,
                group.Version,
                group.Revision,
                sourceVersion));
        }
        catch (ConcurrencyException)
        {
            session.ClearChanges();
            return Conflict();
        }
    }

    private static Dictionary<string, JsonElement> CopyForkableFields(
        IReadOnlyDictionary<string, JsonElement> source,
        IReadOnlyList<ContentFieldDefinition> definitions) => definitions
        .Where(field => field.LocalizationMode == ContentFieldLocalizationMode.CopyOnFork)
        .Where(field => source.ContainsKey(field.Name))
        .ToDictionary(field => field.Name, field => source[field.Name].Clone(), StringComparer.Ordinal);

    private static ContentItem Clone(ContentItem source) => new()
    {
        Id = source.Id,
        Version = source.Version,
        SiteId = source.SiteId,
        ContentTypeAlias = source.ContentTypeAlias,
        Slug = source.Slug,
        Title = source.Title,
        TranslationGroupId = source.TranslationGroupId,
        Culture = source.Culture,
        SourceItemId = source.SourceItemId,
        ParentId = source.ParentId,
        SortOrder = source.SortOrder,
        Fields = source.Fields.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), source.Fields.Comparer),
        PublicationState = source.PublicationState,
        PublishedOn = source.PublishedOn,
        VersionNumber = source.VersionNumber,
        SchedulePublishUtc = source.SchedulePublishUtc,
        ScheduleUnpublishUtc = source.ScheduleUnpublishUtc,
        CreatedOn = source.CreatedOn,
        ModifiedOn = source.ModifiedOn,
        CreatedBy = source.CreatedBy,
        ModifiedBy = source.ModifiedBy,
        TranslationProvenance = source.TranslationProvenance,
        TranslationReview = source.TranslationReview
    };

    private static string? ValidateContextCulture(ContentLocalizationContext context, string? culture)
    {
        if (context.SiteId <= 0 || context.SupportedCultures.Count == 0 || string.IsNullOrWhiteSpace(culture)) return null;
        var canonical = CultureInfo.GetCultureInfo(culture.Trim()).Name;
        return context.SupportedCultures.Select(value => CultureInfo.GetCultureInfo(value).Name)
                .Contains(canonical, StringComparer.OrdinalIgnoreCase)
            ? canonical
            : null;
    }

    private static bool RequiredMatch(long? expected, long actual) => expected is >= 0 && expected == actual;
    private static Result<ContentLocalizationOperationResult, AeroError> Conflict() =>
        Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.ConflictError("The content translation changed. Reload and try again."));

    private async Task<ContentTranslationGroupDocument?> LoadExactGroupAsync(
        ContentLocalizationContext context,
        ContentItem source,
        ContentItem target,
        CancellationToken cancellationToken)
    {
        if (target.TranslationGroupId is not { } groupId)
            return null;

        var group = await session.LoadAsync<ContentTranslationGroupDocument>(groupId, cancellationToken);
        return group is not null
               && group.SiteId == context.SiteId
               && string.Equals(group.ContentTypeAlias, source.ContentTypeAlias, StringComparison.OrdinalIgnoreCase)
               && group.SourceItemId == source.Id
               && target.TranslationGroupId == group.Id
            ? group
            : null;
    }

    private static Result<ContentLocalizationOperationResult, AeroError> ToResult(
        ContentItem item,
        IDocumentVersionFence sourceFence,
        IDocumentVersionFence? groupFence,
        long insertedGroupStorageVersion,
        int groupRevision)
    {
        if (sourceFence.Status != VersionFenceStatus.Committed || sourceFence.CommittedVersion is not { } sourceVersion
            || groupFence is { Status: not VersionFenceStatus.Committed }
            || groupFence is { CommittedVersion: null })
            return Conflict();

        return Prelude.Ok<ContentLocalizationOperationResult, AeroError>(new(
            item.Id,
            item.TranslationGroupId!.Value,
            item.Culture,
            item.TranslationReview.Status,
            item.Version,
            groupFence?.CommittedVersion ?? insertedGroupStorageVersion,
            groupRevision,
            sourceVersion));
    }
}
