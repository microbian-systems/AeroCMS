using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// <see cref="IStartupFilter"/> that registers <see cref="SiteResolutionMiddleware"/>
/// via <c>app.UseMiddleware&lt;SiteResolutionMiddleware&gt;()</c>.
///
/// Registered via <c>services.Insert(0, ...)</c> in <see cref="SitesModule"/>.
/// SitesModule has the lowest <see cref="IAeroModule.Order"/> (-9999), so its
/// <see cref="ConfigureServices"/> runs first and its <see cref="Insert"/> at
/// index 0 makes it the outermost wrapper in the ASP.NET Core pipeline.
/// </summary>
public sealed class SiteStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<SiteResolutionMiddleware>();
            next(app);
        };
    }
}
