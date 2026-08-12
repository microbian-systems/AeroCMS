using System.Text.Json;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Content;
using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Content.Localization;

/// <summary>Bounded, host-supplied context that may accompany an AI translation request.</summary>
public sealed record ContentTranslationContextContribution(string Key, string Value);

/// <summary>Lets a consuming host add small, non-content translation context without coupling the AI module to its domain.</summary>
public interface IContentTranslationContextContributor
{
    Task<Result<IReadOnlyList<ContentTranslationContextContribution>>> ContributeAsync(
        long siteId,
        string contentTypeAlias,
        string sourceCulture,
        string targetCulture,
        CancellationToken cancellationToken = default);
}

/// <summary>Host-owned authorization boundary for site-scoped AI translation generation.</summary>
public interface IContentTranslationSiteAuthorizer
{
    Task<Result<NoneType, AeroError>> AuthorizeAsync(long siteId, CancellationToken cancellationToken = default);
}

/// <summary>Identifies the source variant and its field values for a generated translation.</summary>
public sealed record ContentTranslationSource(
    long ContentItemId,
    int VersionNumber,
    IReadOnlyDictionary<string, JsonElement> Fields);

/// <summary>Identifies the target variant and the version that an eventual apply operation must match.</summary>
public sealed record ContentTranslationTarget(
    long ContentItemId,
    int ExpectedVersionNumber);

/// <summary>Requests a schema-whitelisted AI translation suggestion. This operation never persists or publishes content.</summary>
public sealed record GenerateContentAiTranslationRequest(
    long SiteId,
    ContentTypeDefinition ContentType,
    ContentLocalizationContext Localization,
    ContentTranslationSource Source,
    ContentTranslationTarget Target,
    string SourceCulture,
    string TargetCulture,
    string? ProviderId = null);

/// <summary>Returns a reviewable application command with provider/model provenance and no publication side effect.</summary>
public sealed record GenerateContentAiTranslationResponse(
    ApplyContentAiTranslationCommand Application,
    IReadOnlyList<string> Warnings);

/// <summary>Generates schema-aware, reviewable content translations without accessing content persistence directly.</summary>
public interface IContentAiTranslationGenerationService
{
    Task<Result<GenerateContentAiTranslationResponse>> GenerateAsync(
        GenerateContentAiTranslationRequest request,
        CancellationToken cancellationToken = default);
}
