using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Wraps the host pipeline with <see cref="SiteResolutionMiddleware"/>.
/// </summary>
/// <remarks>
/// <see cref="SitesModule"/> registers this filter at the start of the startup-filter collection,
/// allowing host-based site context to be established before downstream public-site middleware.
/// </remarks>
public sealed class SiteStartupFilter : IStartupFilter
{
    /// <summary>
    /// Produces the application-builder callback that inserts site resolution before the next filter.
    /// </summary>
    /// <param name="next">The remaining startup-filter configuration callback.</param>
    /// <returns>A callback that adds site resolution and then invokes <paramref name="next"/>.</returns>
public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<SiteResolutionMiddleware>();
            next(app);
        };
    }
}
