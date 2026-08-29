using Aero.Cms.Abstractions.Content;
using Aero.Core.Railway;

namespace Aero.Cms.Web.Core.PublicApi;

/// <summary>A bounded page of published query results.</summary>
public sealed record PublicQueryPage<T>(
    IReadOnlyList<T> Items,
    long TotalItems,
    int Skip,
    int Take);

/// <summary>Published page metadata exposed by the read-only query API.</summary>
public sealed record PublicPageQueryItem(
    string Id,
    string Title,
    string Slug,
    string Path,
    string? Summary,
    string Culture,
    DateTimeOffset? PublishedOn);

/// <summary>Published post metadata exposed by the read-only query API.</summary>
public sealed record PublicPostQueryItem(
    string Id,
    string Title,
    string Slug,
    string? Excerpt,
    string Culture,
    string? ImageUrl,
    DateTimeOffset? PublishedOn);

/// <summary>Published documentation metadata exposed by the read-only query API.</summary>
public sealed record PublicDocsQueryItem(
    string Id,
    string Title,
    string Slug,
    string? Summary,
    string Culture,
    string? ParentId,
    int Order,
    DateTimeOffset? PublishedOn);

public sealed record PublicContentSearchItem(
    string Id,
    string Title,
    string Slug,
    string Path,
    string Culture,
    DateTimeOffset? PublishedOn);

public sealed record PublicContentSearchResult(
    IReadOnlyList<PublicContentSearchItem> Items,
    int Skip,
    int Take,
    bool HasMore);

/// <summary>
/// Provides fresh, public-only CMS projections for API and HTMX callers.
/// Implementations own persistence access; callers receive no session or lazy query.
/// </summary>
public interface IPublicCmsQueryService
{
    Task<Result<PublicQueryPage<PublicPageQueryItem>>> QueryPagesAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<Result<PublicQueryPage<PublicPostQueryItem>>> QueryPostsAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<Result<PublicQueryPage<PublicDocsQueryItem>>> QueryDocsAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<Result<ContentQueryResult>> QueryContentAsync(
        string contentTypeAlias,
        ContentTraversal traversal,
        string? rootId,
        int maximumDepth,
        int maximumItems,
        IReadOnlyList<string>? projection,
        CancellationToken cancellationToken = default);

    Task<Result<PublicContentSearchResult>> QueryContentSearchAsync(
        string contentTypeAlias,
        string query,
        Aero.Cms.Core.Content.Search.ContentSearchMode mode,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
