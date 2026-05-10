using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// <see cref="IStartupFilter"/> that registers the URL rewrite middleware
/// in the ASP.NET Core pipeline. Runs AFTER <see cref="Sites.SiteStartupFilter"/>
/// so the current site is already resolved when <see cref="AliasRewriteRule"/> runs.
///
/// Registered via <c>services.Insert(0, ...)</c> in <see cref="AliasModule"/>.
/// </summary>
public sealed class AliasStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            // URL Rewrite — site-scoped alias resolution
            var rule = app.ApplicationServices.GetRequiredService<AliasRewriteRule>();
            var rewriteOptions = new RewriteOptions().Add(rule);
            app.UseRewriter(rewriteOptions);

            // Continue the pipeline (routing, auth, etc.)
            next(app);
        };
    }
}
