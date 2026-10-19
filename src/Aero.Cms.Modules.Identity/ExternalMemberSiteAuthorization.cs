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
public sealed class ExternalMemberPermissionRequirement(string permission) : IAuthorizationRequirement { public string Permission { get; } = permission; }

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
public sealed class ExternalMemberPermissionAuthorizationHandler(ISiteContext siteContext, IQuerySession querySession) : AuthorizationHandler<ExternalMemberPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ExternalMemberPermissionRequirement requirement)
    { try { if (!ExternalMemberPrincipal.TryRead(context.User, out var claims) || siteContext is not { TenantId: > 0, SiteId: > 0 }) return; var assignment = await querySession.Query<ExternalMemberSiteAssignment>().FirstOrDefaultAsync(x => x.ExternalMemberId == claims.MemberId && x.TenantId == siteContext.TenantId && x.SiteId == siteContext.SiteId && x.IsActive); if (assignment is not null && ExternalMemberPermissions.Allows(assignment.CustomerRole, requirement.Permission)) context.Succeed(requirement); } catch { } }
}
public static class ExternalMemberPermissions
{
    public const string PaidAnalysisRequest = "paid-analysis.request"; public const string PaidAnalysisApprove = "paid-analysis.approve"; public const string MemberUsersManage = "member.users.manage";
    public static bool Allows(ExternalMemberCustomerRole role, string permission) => permission switch { PaidAnalysisRequest when role is ExternalMemberCustomerRole.GeneralUser or ExternalMemberCustomerRole.PowerUser or ExternalMemberCustomerRole.CustomerAdmin => true, PaidAnalysisApprove => role is ExternalMemberCustomerRole.PowerUser or ExternalMemberCustomerRole.CustomerAdmin, MemberUsersManage => role is ExternalMemberCustomerRole.CustomerAdmin, _ => false };
}
