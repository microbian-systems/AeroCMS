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
        if (command.ExpectedGroupStorageVersion is null || command.ExpectedGroupStorageVersion <= 0)
            return Conflict();
        if (sourceOk.Value.TranslationGroupId is { } existingGroupId)
        {
            var existingGroup = await session.LoadAsync<ContentTranslationGroupDocument>(existingGroupId, cancellationToken);
            if (existingGroup?.Version != command.ExpectedGroupStorageVersion)
                return Conflict();
            session.UpdateExpectedVersion(existingGroup!, command.ExpectedGroupStorageVersion.Value);
        }

        var type = await contentTypeService.GetByAliasAsync(context.SiteId, sourceOk.Value.ContentTypeAlias, cancellationToken);
        if (type is not Result<ContentTypeDefinition, AeroError>.Ok typeOk)
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.NotFoundError("The source content type was not found."));

        var groupId = sourceOk.Value.TranslationGroupId ?? sourceOk.Value.Id;
        var existing = await session.Query<ContentItem>()
            .FirstOrDefaultAsync(item => item.SiteId == context.SiteId
                && item.ContentTypeAlias == sourceOk.Value.ContentTypeAlias
                && item.TranslationGroupId == groupId
                && item.Culture == targetCulture, cancellationToken);
        if (existing is not null && !command.OverwriteExisting)
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(
                AeroError.ConflictError("A variant already exists for the requested culture."));
        if (existing is not null && (command.ExpectedTargetStorageVersion is null || command.ExpectedTargetStorageVersion <= 0 || existing.Version != command.ExpectedTargetStorageVersion))
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

        var saved = await writableContentService.SaveLocalizationAsync(fork, cancellationToken);
        return saved is Result<ContentItem, AeroError>.Ok savedOk
            ? await ToResultAsync(savedOk.Value, cancellationToken)
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
        session.UpdateExpectedVersion(sourceOk.Value, command.ExpectedSourceStorageVersion ?? sourceOk.Value.Version);
        session.UpdateExpectedVersion(targetOk.Value, command.ExpectedTargetStorageVersion ?? targetOk.Value.Version);
        var group = await session.LoadAsync<ContentTranslationGroupDocument>(targetOk.Value.TranslationGroupId!.Value, cancellationToken);
        if (group is null || !RequiredMatch(command.ExpectedGroupStorageVersion, group.Version)) return Conflict();
        session.UpdateExpectedVersion(group, command.ExpectedGroupStorageVersion ?? group.Version);

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
        targetOk.Value.VersionNumber++;

        var draftValidation = await validation.ValidateAsync(targetOk.Value, ContentValidationMode.Draft, cancellationToken);
        if (draftValidation is Result<ContentItem, AeroError>.Failure validationFailure)
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(validationFailure.Error);

        var saved = await writableContentService.SaveLocalizationAsync(targetOk.Value, cancellationToken);
        return saved is Result<ContentItem, AeroError>.Ok savedOk
            ? await ToResultAsync(savedOk.Value, cancellationToken)
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
        session.UpdateExpectedVersion(sourceOk.Value, command.ExpectedSourceStorageVersion ?? sourceOk.Value.Version);
        session.UpdateExpectedVersion(targetOk.Value, command.ExpectedTargetStorageVersion ?? targetOk.Value.Version);
        var group = await session.LoadAsync<ContentTranslationGroupDocument>(targetOk.Value.TranslationGroupId!.Value, cancellationToken);
        if (group is null || !RequiredMatch(command.ExpectedGroupStorageVersion, group.Version)) return Conflict();
        session.UpdateExpectedVersion(group, command.ExpectedGroupStorageVersion ?? group.Version);

        targetOk.Value.TranslationReview = command.Approved
            ? ContentTranslationReview.Approve(sourceOk.Value.Id, sourceOk.Value.VersionNumber, targetOk.Value.VersionNumber, DateTimeOffset.UtcNow, notes: command.Notes)
            : ContentTranslationReview.Reject(sourceOk.Value.Id, sourceOk.Value.VersionNumber, targetOk.Value.VersionNumber, DateTimeOffset.UtcNow, notes: command.Notes);
        var saved = await writableContentService.SaveLocalizationAsync(targetOk.Value, cancellationToken);
        return saved is Result<ContentItem, AeroError>.Ok savedOk
            ? await ToResultAsync(savedOk.Value, cancellationToken)
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
        var type = await contentTypeService.GetByAliasAsync(context.SiteId, group.ContentTypeAlias, cancellationToken);
        if (type is not Result<ContentTypeDefinition, AeroError>.Ok typeOk
            || command.SharedFields.Keys.Any(name => !typeOk.Value.Fields.Any(field => field.Name == name && field.LocalizationMode == ContentFieldLocalizationMode.Shared)))
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.ValidationError(["Only declared shared fields may be changed through the translation group."]));

        session.UpdateExpectedVersion(group, command.ExpectedGroupStorageVersion);
        group.SharedFields = command.SharedFields.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
        group.Revision++;
        group.ModifiedOn = DateTimeOffset.UtcNow;
        session.Store(group);
        session.Store(new ContentTranslationProjectionWorkDocument
        {
            Id = group.Id ^ ((long)group.Revision << 32), SiteId = group.SiteId, TranslationGroupId = group.Id,
            GroupStorageVersion = group.Version + 1, GroupRevision = group.Revision
        });
        try
        {
            await session.SaveChangesAsync(cancellationToken);
            return Prelude.Ok<ContentLocalizationOperationResult, AeroError>(new(group.SourceItemId, group.Id, group.SourceCulture, ContentTranslationReviewStatus.NotRequired, group.SourceItemId, group.Version, group.Revision));
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

    private static string? ValidateContextCulture(ContentLocalizationContext context, string? culture)
    {
        if (context.SiteId <= 0 || context.SupportedCultures.Count == 0 || string.IsNullOrWhiteSpace(culture)) return null;
        var canonical = CultureInfo.GetCultureInfo(culture.Trim()).Name;
        return context.SupportedCultures.Select(value => CultureInfo.GetCultureInfo(value).Name)
                .Contains(canonical, StringComparer.OrdinalIgnoreCase)
            ? canonical
            : null;
    }

    private static bool RequiredMatch(long? expected, long actual) => expected is > 0 && expected == actual;
    private static Result<ContentLocalizationOperationResult, AeroError> Conflict() =>
        Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.ConflictError("The content translation changed. Reload and try again."));

    private async Task<Result<ContentLocalizationOperationResult, AeroError>> ToResultAsync(ContentItem item, CancellationToken ct)
    {
        var group = await session.LoadAsync<ContentTranslationGroupDocument>(item.TranslationGroupId!.Value, ct);
        return group is null
            ? Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.ConflictError("The translation group changed. Reload and try again."))
            : Prelude.Ok<ContentLocalizationOperationResult, AeroError>(new(item.Id, group.Id, item.Culture, item.TranslationReview.Status, item.Version, group.Version, group.Revision));
    }
}
