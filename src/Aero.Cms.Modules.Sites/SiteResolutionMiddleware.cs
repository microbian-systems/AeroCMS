using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Resolves the current site from the request host and sets
/// <see cref="IAeroSiteSlice"/> on <see cref="HttpContext.Features"/>.
/// Short-circuits with 404 if the host does not match any enabled site.
/// </summary>
public sealed class SiteResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ISiteLookupService siteLookup)
    {
        var host = context.Request.Host.Host;
        var normalized = HostNormalizer.Normalize(host);

        var site = await siteLookup.ResolveByHostAsync(normalized);

        if (site is null || !site.IsEnabled)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // Attach site context to features for downstream consumption
        context.Features.Set<IAeroSiteSlice>(new AeroSiteSlice
        {
            SiteId = site.Id,
            TenantId = site.TenantId
        });

        await next(context);
    }
}
