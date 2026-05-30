using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.SiteMap;

public interface ISiteMapService
{
    Task<Result<string, AeroError>> BuildSitemapAsync(CancellationToken ct);
    Task<Result<string, AeroError>> BuildSitemapAsync(string? culture, CancellationToken ct);
    Task<Result<string, AeroError>> BuildSitemapIndexAsync(CancellationToken ct);
}
