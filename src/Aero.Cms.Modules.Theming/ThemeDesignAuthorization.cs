using System.Security.Claims;
using Aero.Cms.Core.Entities;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Theming;

/// <summary>Requires the trusted design capability for the manager's selected site.</summary>
public sealed class ThemeDesignPermissionRequirement : IAuthorizationRequirement;

/// <summary>Resolves the selected-site design permission without treating ordinary update access as code/design trust.</summary>
public sealed class ThemeDesignPermissionHandler(
    IHttpContextAccessor httpContextAccessor,
    IQuerySession querySession) : AuthorizationHandler<ThemeDesignPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ThemeDesignPermissionRequirement requirement)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Fail();
            return;
        }

        if (user.IsInRole("Admin") || user.HasClaim("is_admin", "true"))
        {
            context.Succeed(requirement);
            return;
        }

        if (!long.TryParse(
                httpContextAccessor.HttpContext?.Request.Cookies["AeroCms.SiteId"],
                out var siteId)
            || !long.TryParse(
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value,
                out var userId))
        {
            context.Fail();
            return;
        }

        try
        {
            var assignment = await querySession.Query<UserSiteAssignment>()
                .FirstOrDefaultAsync(item => item.UserId == userId && item.SiteId == siteId);
            if (assignment?.Permissions.Contains("design", StringComparer.OrdinalIgnoreCase) == true)
            {
                context.Succeed(requirement);
                return;
            }
        }
        catch
        {
            // Authorization fails closed when the selected-site assignment cannot be read.
        }

        context.Fail();
    }
}
