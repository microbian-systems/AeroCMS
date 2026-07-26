using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Search;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Provides read-only, site-scoped Sable queries for content items.
/// </summary>
/// <remarks>
/// Current implementations return successful results for completed queries. Cancellation
/// and storage-provider failures are not converted to <see cref="AeroError"/> values and
/// may propagate to the caller.
/// </remarks>
public interface IContentQueryService
{
    /// <summary>Retrieves a page of content items for a content type.</summary>
    /// <param name="siteId">The owning site identifier.</param><param name="contentTypeAlias">The content type alias.</param><param name="skip">The number of matching items to skip.</param><param name="take">The maximum number of items to return.</param><param name="ct">A token that can cancel the operation.</param>
    /// <returns>The requested page and total match count on success; otherwise an <see cref="AeroError"/>.</returns>
    /// <remarks>
    /// Items are ordered by publication timestamp in descending order before paging.
    /// <paramref name="skip"/> and <paramref name="take"/> are passed to the query provider
    /// without service-level range validation.
    /// </remarks>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    Task<Result<(IReadOnlyList<ContentItem> Items, long TotalCount), AeroError>> GetByTypeAsync(
        long siteId, string contentTypeAlias, int skip = 0, int take = 20, CancellationToken ct = default);

    /// <summary>Counts content items for a site and content type.</summary>
    /// <param name="siteId">The owning site identifier.</param><param name="contentTypeAlias">The content type alias.</param><param name="ct">A token that can cancel the operation.</param>
    /// <returns>The matching count on success; otherwise an <see cref="AeroError"/>.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    Task<Result<long, AeroError>> CountByTypeAsync(
        long siteId, string contentTypeAlias, CancellationToken ct = default);

    /// <summary>Searches content items using the supported search filter.</summary>
    /// <param name="siteId">The owning site identifier.</param><param name="contentTypeAlias">The content type alias.</param><param name="fieldFilters">The field filters to apply.</param><param name="ct">A token that can cancel the operation.</param>
    /// <returns>The matching items, ordered by publication timestamp in descending order.</returns>
    /// <remarks>
    /// The <c>__search</c> entry performs case-insensitive text matching, <c>__culture</c>
    /// restricts the culture, and remaining entries apply exact field-value filters. The
    /// site/type result set is loaded before these provider-neutral filters are applied.
    /// </remarks>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled while loading the result set.</exception>
    Task<Result<IReadOnlyList<ContentItem>, AeroError>> SearchAsync(
        long siteId, string contentTypeAlias, Dictionary<string, string> fieldFilters, CancellationToken ct = default);

    /// <summary>Runs a bounded query against the persisted exact, full-text, or semantic projections.</summary>
    Task<Result<ContentSearchResult>> SearchIndexAsync(
        ContentSearchRequest request,
        CancellationToken ct = default);

    /// <summary>Lists the culture variants that share a translation group.</summary>
    /// <param name="siteId">The owning site identifier.</param><param name="contentTypeAlias">The content type alias.</param><param name="translationGroupId">The translation group identifier.</param><param name="ct">A token that can cancel the operation.</param>
    /// <returns>The matching variants on success; otherwise an <see cref="AeroError"/>.</returns>
    /// <remarks>
    /// A variant matches when its translation-group identifier or its own identifier equals
    /// <paramref name="translationGroupId"/>. Results are ordered by culture.
    /// </remarks>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    Task<Result<IReadOnlyList<ContentItem>, AeroError>> ListCultureVariantsAsync(
        long siteId, string contentTypeAlias, long translationGroupId, CancellationToken ct = default);
}
