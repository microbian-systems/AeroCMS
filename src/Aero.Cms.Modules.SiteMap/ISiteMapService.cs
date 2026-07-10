using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.SiteMap;

/// <summary>
/// Defines an interface for ISiteMapService.
/// </summary>
public interface ISiteMapService
{
        /// <summary>
    /// BuildSitemapAsync method.
    /// </summary>
Task<Result<string, AeroError>> BuildSitemapAsync(CancellationToken ct);
        /// <summary>
    /// BuildSitemapAsync method.
    /// </summary>
Task<Result<string, AeroError>> BuildSitemapAsync(string? culture, CancellationToken ct);
        /// <summary>
    /// BuildSitemapIndexAsync method.
    /// </summary>
Task<Result<string, AeroError>> BuildSitemapIndexAsync(CancellationToken ct);
}
