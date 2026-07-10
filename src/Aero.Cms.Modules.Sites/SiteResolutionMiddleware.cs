using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Resolves the current site from the request host and sets
/// <see cref="IAeroSiteSlice"/> on <see cref="HttpContext.Features"/>.
/// Short-circuits with 404 if the host does not match any enabled site.
///
/// Skips <c>/manager/*</c> routes — the manager resolves the site from the
/// <c>AeroCms.SiteId</c> cookie set by explicit user selection.
/// </summary>
public sealed class SiteResolutionMiddleware(RequestDelegate next)
{
    private static readonly PathString ManagerPathPrefix = "/manager";
    private static readonly PathString AdminApiPathPrefix = "/api/v1/admin";
    private static readonly PathString NoSitePathPrefix = "/nosite";

        /// <summary>
    /// InvokeAsync method.
    /// </summary>
public async Task InvokeAsync(
        HttpContext context,
        ISiteLookupService siteLookup)
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

        var site = await siteLookup.ResolveByHostAsync(normalized);

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
