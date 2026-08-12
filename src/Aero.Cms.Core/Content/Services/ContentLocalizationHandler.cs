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

        var saved = await contentService.SaveAsync(fork, cancellationToken);
        return saved is Result<ContentItem, AeroError>.Ok savedOk
            ? Prelude.Ok<ContentLocalizationOperationResult, AeroError>(new(
                savedOk.Value.Id, savedOk.Value.TranslationGroupId!.Value, savedOk.Value.Culture,
                savedOk.Value.TranslationReview.Status))
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

        var type = await contentTypeService.GetByAliasAsync(context.SiteId, targetOk.Value.ContentTypeAlias, cancellationToken);
        if (type is not Result<ContentTypeDefinition, AeroError>.Ok typeOk)
            return Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.NotFoundError("The target content type was not found."));

        var allowed = typeOk.Value.Fields
            .Where(field => field.LocalizationMode != ContentFieldLocalizationMode.Shared)
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

        var saved = await contentService.SaveAsync(targetOk.Value, cancellationToken);
        return saved is Result<ContentItem, AeroError>.Ok savedOk
            ? Prelude.Ok<ContentLocalizationOperationResult, AeroError>(new(
                savedOk.Value.Id, savedOk.Value.TranslationGroupId!.Value, savedOk.Value.Culture,
                savedOk.Value.TranslationReview.Status))
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

        targetOk.Value.TranslationReview = command.Approved
            ? ContentTranslationReview.Approve(sourceOk.Value.Id, sourceOk.Value.VersionNumber, targetOk.Value.VersionNumber, DateTimeOffset.UtcNow, notes: command.Notes)
            : ContentTranslationReview.Reject(sourceOk.Value.Id, sourceOk.Value.VersionNumber, targetOk.Value.VersionNumber, DateTimeOffset.UtcNow, notes: command.Notes);
        var saved = await contentService.SaveAsync(targetOk.Value, cancellationToken);
        return saved is Result<ContentItem, AeroError>.Ok savedOk
            ? Prelude.Ok<ContentLocalizationOperationResult, AeroError>(new(savedOk.Value.Id, savedOk.Value.TranslationGroupId!.Value, savedOk.Value.Culture, savedOk.Value.TranslationReview.Status))
            : Prelude.Fail<ContentLocalizationOperationResult, AeroError>(AeroError.ConflictError("The translation review could not be saved."));
    }

    private static Dictionary<string, JsonElement> CopyForkableFields(
        IReadOnlyDictionary<string, JsonElement> source,
        IReadOnlyList<ContentFieldDefinition> definitions) => definitions
        .Where(field => field.LocalizationMode == ContentFieldLocalizationMode.CopyOnFork)
        .Where(field => source.ContainsKey(field.Name))
        .ToDictionary(field => field.Name, field => source[field.Name].Clone(), StringComparer.Ordinal);

    private static string? ValidateContextCulture(ContentLocalizationContext context, string? culture)
    {
        if (context.SiteId <= 0 || string.IsNullOrWhiteSpace(culture)) return null;
        var canonical = CultureInfo.GetCultureInfo(culture.Trim()).Name;
        return context.SupportedCultures.Count == 0
            || context.SupportedCultures.Select(value => CultureInfo.GetCultureInfo(value).Name)
                .Contains(canonical, StringComparer.OrdinalIgnoreCase)
            ? canonical
            : null;
    }
}
