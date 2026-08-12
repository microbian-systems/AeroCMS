using Aero.Core.Railway;
#if !AERO_CMS_BROWSER_CLIENT
using System.Text.Json;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Content;
#endif

namespace Aero.Cms.Abstractions.Content.Localization;

/// <summary>Browser-safe identity and concurrency assertions for a translation generation request.</summary>
public sealed record GenerateContentAiTranslationRequest(
    long SiteId,
    long SourceItemId,
    int SourceVersionNumber,
    long TargetItemId,
    int ExpectedTargetVersionNumber,
    string TargetCulture,
    string? ProviderId = null);

/// <summary>Reviewable, non-persisted translation proposal returned to browser clients.</summary>
public sealed record GenerateContentAiTranslationResponse(ApplyContentAiTranslationCommand Application, IReadOnlyList<string> Warnings);

#if !AERO_CMS_BROWSER_CLIENT
/// <summary>Bounded, host-supplied context that may accompany an AI translation request.</summary>
public sealed record ContentTranslationContextContribution(string Key, string Value);

public interface IContentTranslationContextContributor
{
    Task<Result<IReadOnlyList<ContentTranslationContextContribution>>> ContributeAsync(long siteId, string contentTypeAlias, string sourceCulture, string targetCulture, CancellationToken cancellationToken = default);
}

/// <summary>Host-owned authorization boundary for site-scoped AI translation generation.</summary>
public interface IContentTranslationSiteAuthorizer
{
    Task<Result<NoneType, AeroError>> AuthorizeAsync(long siteId, CancellationToken cancellationToken = default);
}

/// <summary>Trusted, site-scoped source data loaded server-side before any provider call.</summary>
public sealed record ContentTranslationSource(long ContentItemId, long SiteId, string ContentTypeAlias, long TranslationGroupId, int VersionNumber, string Culture, IReadOnlyDictionary<string, JsonElement> Fields);
public sealed record ContentTranslationTarget(long ContentItemId, long SiteId, string ContentTypeAlias, long TranslationGroupId, int VersionNumber, string Culture);
public sealed record ContentAiTranslationGenerationSnapshot(ContentTypeDefinition ContentType, ContentLocalizationContext Localization, ContentTranslationSource Source, ContentTranslationTarget Target);

/// <summary>Host/persistence boundary that resolves an authoritative translation snapshot; client schema or content is never accepted.</summary>
public interface IContentAiTranslationSnapshotResolver
{
    Task<Result<ContentAiTranslationGenerationSnapshot>> ResolveAsync(long siteId, long sourceItemId, long targetItemId, CancellationToken cancellationToken = default);
}

public interface IContentAiTranslationGenerationService
{
    Task<Result<GenerateContentAiTranslationResponse>> GenerateAsync(GenerateContentAiTranslationRequest request, CancellationToken cancellationToken = default);
}
#endif
