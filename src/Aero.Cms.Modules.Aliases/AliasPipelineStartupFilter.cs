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
public sealed class AliasPipelineStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            // 1. URL Rewrite — resolves aliases BEFORE anything else
            var rule = app.ApplicationServices.GetRequiredService<AliasRewriteRule>();
            var rewriteOptions = new RewriteOptions().Add(rule);
            app.UseRewriter(rewriteOptions);

            // 2. Error status code handling (404 + 5xx)
            //    API paths → pass through (minimal APIs return their own error responses)
            //    Browser paths → redirect to /oops CMS page (seeded at startup)
            app.UseStatusCodePages(async context =>
            {
                var code = context.HttpContext.Response.StatusCode;
                var path = context.HttpContext.Request.Path;

                // API routes handle their own error responses
                if (path.StartsWithSegments("/api")) return;

                // 404 and 5xx → friendly CMS error page
                if (code == 404 || (code >= 500 && code <= 599))
                {
                    context.HttpContext.Response.Redirect($"/oops?status={code}");
                }
            });

            // 3. Continue the pipeline (routing, auth, etc.)
            next(app);
        };
    }
}
