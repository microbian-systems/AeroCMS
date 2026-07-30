using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Ai.Knowledge;

/// <summary>Stable source-kind identifiers stored with AI knowledge projections.</summary>
public static class AeroAiKnowledgeSourceKinds
{
    public const string Page = "page";
    public const string Post = "post";
    public const string Docs = "docs";
    public const string ContentItem = "content-item";
    public const string CommerceProduct = "commerce-product";
    public const string AeroDocumentation = "aero-documentation";
}

/// <summary>
/// One semantically meaningful source section before bounded chunking.
/// </summary>
public sealed record AeroAiKnowledgeSection(
    string Name,
    string Content,
    AeroAiFieldExposure Exposure = AeroAiFieldExposure.Internal);

/// <summary>
/// A normalized, source-agnostic request to replace all knowledge chunks for one CMS record.
/// </summary>
/// <remarks>
/// Public and manager sections are deliberately separate. This permits a page to project its
/// published snapshot publicly while projecting its newer draft only to the manager corpus.
/// </remarks>
public sealed record AeroAiKnowledgeSource(
    long TenantId,
    long SiteId,
    string SourceKind,
    long SourceId,
    string SourceUri,
    string Culture,
    long SourceRevision,
    bool IsPublished,
    bool IncludeInSearch,
    bool IncludeInPublicAi,
    string Title,
    IReadOnlyList<AeroAiKnowledgeSection> PublicSections,
    IReadOnlyList<AeroAiKnowledgeSection> ManagerSections);

/// <summary>Stages disposable knowledge projections in the caller's Sable unit of work.</summary>
public interface IAeroAiKnowledgeProjectionService
{
    Task StageUpsertAsync(
        AeroAiKnowledgeSource source,
        CancellationToken cancellationToken = default);

    Task StageDeleteAsync(
        long tenantId,
        long siteId,
        string sourceKind,
        long sourceId,
        CancellationToken cancellationToken = default);
}

/// <summary>A bounded, security-scoped hybrid knowledge retrieval request.</summary>
public sealed record AeroAiKnowledgeQuery(
    long TenantId,
    long SiteId,
    AeroAiAudience Audience,
    string Culture,
    string Query,
    int Take = 8);

/// <summary>One citation-bearing knowledge result returned after authorization filtering.</summary>
public sealed record AeroAiKnowledgeMatch(
    long ChunkId,
    string SourceKind,
    long SourceId,
    string SourceUri,
    string Culture,
    string Title,
    string Section,
    string Content,
    long SourceRevision,
    int ChunkRevision,
    string ContentHash);

/// <summary>A display-safe public search result from the explicitly enabled AI corpus.</summary>
public sealed record AeroAiPublicSearchItem(
    string SourceKind,
    string SourceId,
    string SourceUri,
    string Title,
    string Section,
    string Excerpt);

/// <summary>A bounded public AI-corpus search response.</summary>
public sealed record AeroAiPublicSearchResult(
    IReadOnlyList<AeroAiPublicSearchItem> Items,
    string Culture);

/// <summary>Retrieves only knowledge admitted to the requested trust plane and site scope.</summary>
public interface IAeroAiKnowledgeRetriever
{
    Task<Result<IReadOnlyList<AeroAiKnowledgeMatch>>> SearchAsync(
        AeroAiKnowledgeQuery query,
        CancellationToken cancellationToken = default);
}
