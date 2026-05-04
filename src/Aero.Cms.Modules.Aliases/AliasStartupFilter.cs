using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// <see cref="IStartupFilter"/> that registers the URL rewrite middleware
/// and error status code handler BEFORE all other middleware in the pipeline.
///
/// Registered via <c>services.Insert(0, ...)</c> in <see cref="AliasModule"/>
/// to guarantee this filter wraps the entire request pipeline.
/// </summary>
public sealed class AliasStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseStatusCodePagesWithRedirects("/oops");

            // 1. URL Rewrite — resolves aliases BEFORE anything else
            var rule = app.ApplicationServices.GetRequiredService<AliasRewriteRule>();
            var rewriteOptions = new RewriteOptions().Add(rule);
            app.UseRewriter(rewriteOptions);

            // 3. Continue the pipeline (routing, auth, etc.)
            next(app);
        };
    }
}
