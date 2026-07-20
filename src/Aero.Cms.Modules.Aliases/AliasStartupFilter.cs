using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Startup filter that registers <see cref="AliasRewriteRule"/> with URL rewriting.
/// Alias resolution requires site-resolution middleware to have populated the
/// current-site feature first. The module registers this filter with a service
/// descriptor ordering intended to achieve that relationship, but applications
/// composing additional startup filters must verify their resulting pipeline.
/// </summary>
public sealed class AliasStartupFilter : IStartupFilter
{
    /// <summary>Wraps the next pipeline configuration with alias URL rewriting.</summary>
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
