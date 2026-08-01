using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Prevents access to the normal application until bootstrap configuration is available.
/// </summary>
/// <remarks>
/// Allowlisted paths and requests made after setup is considered complete continue through
/// the pipeline. Other GET and HEAD requests receive a temporary redirect to <c>/setup</c>;
/// non-idempotent methods receive 404 so request bodies are not replayed against the setup route.
/// </remarks>
public sealed class SetupGateMiddleware(
    ISetupInitializationService setupInitializationService,
    SetupPathAllowlist allowlist,
    Bootstrap.RuntimeBootstrapReadinessGate readinessGate) : IMiddleware
{
    /// <inheritdoc />
public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (allowlist.IsAllowed(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (readinessGate.RequiresReadiness)
        {
            if (await readinessGate.WaitAsync(context.RequestAborted))
            {
                await next(context);
                return;
            }

            var statusCodePages = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>();
            if (statusCodePages is not null)
            {
                statusCodePages.Enabled = false;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("Service Unavailable", context.RequestAborted);
            return;
        }

        if (await setupInitializationService.IsSetupCompleteAsync(context.RequestAborted))
        {
            await next(context);
            return;
        }

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
        context.Response.Headers.Location = SetupPathAllowlist.SetupPath;
    }
}

/// <summary>
/// Adds the CMS setup access gate to an ASP.NET Core pipeline.
/// </summary>
public static class SetupApplicationBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="SetupGateMiddleware"/> at the current pipeline position.
    /// </summary>
    /// <param name="app">The application builder to modify.</param>
    /// <returns>The application builder for continued pipeline configuration.</returns>
public static IApplicationBuilder UseCmsSetupGate(this IApplicationBuilder app)
        => app.UseMiddleware<SetupGateMiddleware>();

    /// <summary>
    /// Expires manager site selection while the setup-only host is active.
    /// </summary>
    /// <param name="app">The setup application builder.</param>
    /// <returns>The application builder for continued pipeline configuration.</returns>
    /// <remarks>
    /// Browser local-storage selection is cleared by the setup host's
    /// <c>setup-handoff.js</c> before Blazor starts.
    /// </remarks>
    public static IApplicationBuilder UseSetupSiteSelectionReset(this IApplicationBuilder app)
    {
        return app.Use(static async (context, next) =>
        {
            context.Response.Cookies.Delete("AeroCms.SiteId", new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                IsEssential = true
            });

            await next(context);
        });
    }
}
