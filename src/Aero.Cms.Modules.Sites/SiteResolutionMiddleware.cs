using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Resolves public requests to an enabled site and exposes its tenant and culture slice downstream.
/// </summary>
/// <param name="next">The next request delegate in the host pipeline.</param>
/// <remarks>
/// Manager, admin API, no-site, static-asset, and development-tooling paths bypass host resolution.
/// An unmatched or disabled public host is redirected to <c>/nosite</c>; otherwise an
/// <see cref="IAeroSiteSlice"/> is attached to <see cref="HttpContext.Features"/> before the next
/// delegate runs.
/// </remarks>
public sealed class SiteResolutionMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Identifies manager UI routes whose site comes from explicit user selection.
    /// </summary>
    private static readonly PathString ManagerPathPrefix = "/manager";

    /// <summary>
    /// Identifies administrative API routes that must be reachable before public-site resolution.
    /// </summary>
    private static readonly PathString AdminApiPathPrefix = "/api/v1/admin";

    /// <summary>
    /// Identifies the fallback route displayed when no public site matches.
    /// </summary>
    private static readonly PathString NoSitePathPrefix = "/nosite";

    /// <summary>
    /// Resolves the request host or bypasses resolution for infrastructure and manager paths.
    /// </summary>
    /// <param name="context">The active HTTP request and response context.</param>
    /// <returns>A task that completes after redirection or downstream pipeline execution.</returns>
    /// <remarks>
    /// Request cancellation is passed to the lookup operation. Lookup and downstream exceptions are
    /// not translated by this middleware.
    /// </remarks>
public async Task InvokeAsync(HttpContext context)
    {
        // The manager resolves site from user cookie selection, not hostname.
        if (IsSiteResolutionBypassPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        // The NoSiteExists page must be reachable without a matching site.
        if (context.Request.Path.StartsWithSegments(NoSitePathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var host = context.Request.Host.Host;
        var normalized = HostNormalizer.Normalize(host);
        var siteLookup = context.RequestServices.GetRequiredService<ISiteLookupService>();

        var site = await siteLookup.ResolveByHostAsync(normalized, context.RequestAborted);

        if (site is null || !site.IsEnabled)
        {
            context.Response.Redirect("/nosite");
            return;
        }

        // Attach site context to features for downstream consumption
        context.Features.Set<IAeroSiteSlice>(new AeroSiteSlice
        {
            SiteId = site.Id,
            TenantId = site.TenantId,
            DefaultCulture = site.DefaultCulture,
            SupportedCultures = site.SupportedCultures
        });

        await next(context);
    }

    /// <summary>
    /// Determines whether a path must remain reachable without a resolved public site.
    /// </summary>
    /// <param name="path">The request path to classify.</param>
    /// <returns><see langword="true"/> for manager, API, no-site, asset, and development paths.</returns>
    private static bool IsSiteResolutionBypassPath(PathString path)
    {
        if (path.StartsWithSegments(ManagerPathPrefix, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments(AdminApiPathPrefix, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments(NoSitePathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Static assets and development tooling must be served by the static file
        // and framework middleware, even when no public site matches the request host.
        return path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/_content", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/_vs", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/css", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/js", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/lib", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/media", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/aspnetcore-browser-refresh.js", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/browserLink", StringComparison.OrdinalIgnoreCase);
    }
}
