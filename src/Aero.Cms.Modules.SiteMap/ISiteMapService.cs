using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.SiteMap;

/// <summary>
/// Generates site- and culture-scoped sitemap XML for the current request context.
/// </summary>
public interface ISiteMapService
{
        /// <summary>
    /// Builds a URL set for the current site's default culture.
    /// </summary>
    /// <param name="ct">The token propagated to content queries and cache operations.</param>
    /// <returns>Sitemap XML, or an error when request context or content retrieval fails.</returns>
Task<Result<string, AeroError>> BuildSitemapAsync(CancellationToken ct);
        /// <summary>
    /// Builds a URL set for a supported culture of the current site.
    /// </summary>
    /// <param name="culture">The requested culture; null or invalid values fall back to the site default.</param>
    /// <param name="ct">The token propagated to content queries and cache operations.</param>
    /// <returns>Sitemap XML, or a validation or generation error.</returns>
Task<Result<string, AeroError>> BuildSitemapAsync(string? culture, CancellationToken ct);
        /// <summary>
    /// Builds an index that links to one sitemap for each supported culture.
    /// </summary>
    /// <param name="ct">The token propagated to cache operations.</param>
    /// <returns>Sitemap-index XML, or an error when no active HTTP request is available.</returns>
Task<Result<string, AeroError>> BuildSitemapIndexAsync(CancellationToken ct);
}
