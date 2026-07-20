using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Core.Http;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authorization;

namespace Aero.Cms.Modules.Identity;

/// <summary>Requires an active local external-member assignment for the host-resolved public site.</summary>
public sealed class ExternalMemberSiteRequirement : IAuthorizationRequirement
{
}

/// <summary>Authorizes storefront access without consulting the manager selected-site cookie.</summary>
public sealed class ExternalMemberSiteAuthorizationHandler(
    ISiteContext siteContext,
    IQuerySession querySession) : AuthorizationHandler<ExternalMemberSiteRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ExternalMemberSiteRequirement requirement)
    {
        try
        {
            if (!ExternalMemberPrincipal.TryRead(context.User, out var claims) ||
                siteContext.SiteId <= 0 || siteContext.TenantId <= 0)
            {
                context.Fail();
                return;
            }

            var site = await querySession.LoadAsync<SitesModel>(siteContext.SiteId);
            if (site is null || !site.IsEnabled || site.TenantId != siteContext.TenantId)
            {
                context.Fail();
                return;
            }

            var membership = await querySession.Query<ExternalMemberSiteAssignment>()
                .FirstOrDefaultAsync(assignment => assignment.ExternalMemberId == claims.MemberId &&
                    assignment.SiteId == site.Id && assignment.TenantId == site.TenantId && assignment.IsActive);

            if (membership is not null)
                context.Succeed(requirement);
            else
                context.Fail();
        }
        catch
        {
            context.Fail();
        }
    }
}
