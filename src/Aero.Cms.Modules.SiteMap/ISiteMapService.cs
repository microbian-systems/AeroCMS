using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.SiteMap;

public interface ISiteMapService
{
    Task<Result<string, AeroError>> BuildSitemapAsync(CancellationToken ct);
}
