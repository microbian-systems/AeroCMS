using System.Globalization;
using System.Text.Json;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Ai.Services;

/// <summary>Fails closed until the consuming host supplies a site authorization implementation.</summary>
public sealed class DenyContentTranslationSiteAuthorizer : IContentTranslationSiteAuthorizer
{
    public Task<Result<NoneType, AeroError>> AuthorizeAsync(long siteId, CancellationToken cancellationToken = default)
        => Task.FromResult<Result<NoneType, AeroError>>(AeroError.InvalidRequestError("No site authorization policy is registered for AI translation."));
}

/// <summary>Whitelists localized textual fields and produces a non-persisting translation application command.</summary>
public sealed class SchemaAwareContentAiTranslationGenerationService(
    IAiContentTranslationService translationService,
    IEnumerable<IContentTranslationContextContributor> contextContributors,
    IEnumerable<IContentTranslationFieldHandler> fieldHandlers)
    : IContentAiTranslationGenerationService
{
    private const int MaxFields = 40;
    private const int MaxSourceBytes = 200_000;
    private const int MaxContextBytes = 8_000;

    public async Task<Result<GenerateContentAiTranslationResponse>> GenerateAsync(
        GenerateContentAiTranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        var cultures = CanonicalCultures(request.Localization);
        if (cultures is null || !cultures.Contains(request.SourceCulture) || !cultures.Contains(request.TargetCulture)
            || string.Equals(request.SourceCulture, request.TargetCulture, StringComparison.OrdinalIgnoreCase))
        {
            return AeroError.ValidationError(["Source and target cultures must be distinct canonical supported cultures."]);
        }

        if (request.SiteId <= 0 || request.ContentType.SiteId != request.SiteId || request.Localization.SiteId != request.SiteId
            || request.Source.ContentItemId <= 0 || request.Source.VersionNumber <= 0
            || request.Target.ContentItemId <= 0 || request.Target.ExpectedVersionNumber <= 0)
        {
            return AeroError.ValidationError(["Site, item, and version metadata must be positive and site-scoped."]);
        }

        var warnings = new List<string>();
        var fields = new List<TranslateDocumentField>();
        var handlers = fieldHandlers.ToDictionary(handler => handler.FieldType, StringComparer.OrdinalIgnoreCase);
        var selectedHandlers = new Dictionary<string, IContentTranslationFieldHandler>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in request.ContentType.Fields)
        {
            if (field.LocalizationMode != ContentFieldLocalizationMode.Localized || !request.Source.Fields.TryGetValue(field.Name, out var value))
            {
                continue;
            }

            if (handlers.TryGetValue(field.FieldType, out var handler) && handler.TryCreate(field, value, out var translationField))
            {
                fields.Add(translationField);
                selectedHandlers[field.Name] = handler;
            }
            else
            {
                warnings.Add($"Localized field '{field.Name}' of type '{field.FieldType}' is unsupported and was not sent to AI.");
            }
        }

        if (fields.Count == 0)
        {
            return AeroError.ValidationError(["The schema contains no supported localized fields to translate."]);
        }
        if (fields.Count > MaxFields || fields.Sum(field => System.Text.Encoding.UTF8.GetByteCount(field.SourceText)) > MaxSourceBytes)
        {
            return AeroError.ValidationError(["Translation request exceeds the field or source-byte limit."]);
        }

        var contextBytes = 0;
        var context = new List<ContentTranslationPromptContext>();
        foreach (var contributor in contextContributors)
        {
            var contribution = await contributor.ContributeAsync(request.SiteId, request.ContentType.Alias, request.SourceCulture, request.TargetCulture, cancellationToken);
            if (contribution is Result<IReadOnlyList<ContentTranslationContextContribution>>.Failure failure)
            {
                return failure.Error;
            }
            var items = ((Result<IReadOnlyList<ContentTranslationContextContribution>>.Ok)contribution).Value;
            contextBytes += items.Sum(item => System.Text.Encoding.UTF8.GetByteCount(item.Key) + System.Text.Encoding.UTF8.GetByteCount(item.Value));
            if (contextBytes > MaxContextBytes)
            {
                return AeroError.ValidationError(["Translation context exceeds the byte limit."]);
            }
            context.AddRange(items.Select(item => new ContentTranslationPromptContext(item.Key, item.Value)));
        }

        var translated = await translationService.TranslateAsync(new TranslateDocumentRequest(fields, request.SourceCulture, request.TargetCulture, request.ProviderId, context), cancellationToken);
        if (translated is Result<TranslateDocumentResponse>.Failure providerFailure)
        {
            return providerFailure.Error;
        }
        var providerResponse = ((Result<TranslateDocumentResponse>.Ok)translated).Value;
        warnings.AddRange(providerResponse.Warnings);
        var output = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            var value = providerResponse.TranslatedFields.TryGetValue(field.Key, out var translatedValue) ? translatedValue : field.SourceText;
            if (!selectedHandlers[field.Key].IsSafeResult(field.SourceText, value))
            {
                warnings.Add($"AI translation for rich-text field '{field.Key}' did not preserve markup and was left unchanged.");
                value = field.SourceText;
            }
            output[field.Key] = JsonSerializer.SerializeToElement(value);
        }

        return new GenerateContentAiTranslationResponse(
            new ApplyContentAiTranslationCommand(request.Source.ContentItemId, request.Source.VersionNumber,
                request.Target.ContentItemId, request.Target.ExpectedVersionNumber, request.SourceCulture,
                request.TargetCulture, output, providerResponse.Provider, providerResponse.Model), warnings);
    }

    private static HashSet<string>? CanonicalCultures(ContentLocalizationContext context)
    {
        try
        {
            var supported = context.SupportedCultures.Select(value => CultureInfo.GetCultureInfo(value).Name).ToHashSet(StringComparer.Ordinal);
            return supported.Contains(CultureInfo.GetCultureInfo(context.DefaultCulture).Name) ? supported : null;
        }
        catch (CultureNotFoundException) { return null; }
    }

    internal static bool PreservesMarkup(string source, string translated)
        => Tags(source).SequenceEqual(Tags(translated), StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> Tags(string value) => System.Text.RegularExpressions.Regex.Matches(value, "<\\/?[a-zA-Z][^>]*>")
        .Select(match => System.Text.RegularExpressions.Regex.Replace(match.Value, "\\s+[^>]*", string.Empty));
}
