using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Core.Infrastructure;

/// <summary>Completes an authorized manager request's tenant/site scope from persisted site storage.</summary>
/// <remarks>
/// Manager APIs bypass public host resolution. The selected site identifier can therefore come
/// from the HTTP-only manager cookie, but the tenant identifier always comes from the server-side
/// site record. Authorization policies execute before endpoint filters and remain authoritative.
/// </remarks>
public sealed class SelectedSiteScopeEndpointFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var siteContext = context.HttpContext.RequestServices.GetRequiredService<ISiteContext>();
        if (new ContentViewScope(siteContext.TenantId, siteContext.SiteId).IsValid)
            return await next(context);

        if (siteContext.SiteId <= 0)
            return NoCurrentSiteSelected();

        var resolver = context.HttpContext.RequestServices.GetService<ISelectedSiteScopeResolver>();
        if (resolver is null)
            return await next(context);

        var resolved = await resolver.ResolveAsync(siteContext.SiteId, context.HttpContext.RequestAborted);
        if (resolved is not { IsValid: true } || resolved.Value.SiteId != siteContext.SiteId)
            return NoCurrentSiteSelected();

        context.HttpContext.Features.Set<IAeroSiteSlice>(new AeroSiteSlice
        {
            TenantId = resolved.Value.TenantId,
            SiteId = resolved.Value.SiteId
        });

        return await next(context);
    }

    private static IResult NoCurrentSiteSelected() => TypedResults.BadRequest(new ProblemDetails
    {
        Title = "No current site selected",
        Detail = "Select an authorized site before managing content.",
        Status = StatusCodes.Status400BadRequest
    });
}
