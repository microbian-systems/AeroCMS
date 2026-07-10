using System.Security.Claims;
using Aero.Cms.Core.Entities;
using AeroDB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Authorization handler for <see cref="SitePermissionRequirement"/>.
///
/// Authorization flow:
/// 1. Is user authenticated? → No → Fail
/// 2. Is user an Admin (has "Admin" role or "is_admin" claim)? → Yes → Succeed (admins have all site permissions)
/// 3. Is there a current site from the "AeroCms.SiteId" cookie? → No → Fail
/// 4. Does the user have the required permission on that site via UserSiteAssignment? → Yes → Succeed
/// 5. Otherwise → Fail
/// </summary>
public sealed class SitePermissionHandler(
    IHttpContextAccessor httpContextAccessor,
    IQuerySession querySession) : AuthorizationHandler<SitePermissionRequirement>
{
    private const string SiteCookieName = "AeroCms.SiteId";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SitePermissionRequirement requirement)
    {
        var user = context.User;

        // 1. Must be authenticated
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Fail();
            return;
        }

        // 2. Admin bypass — admins have all site permissions
        if (user.IsInRole("Admin") || user.HasClaim("is_admin", "true"))
        {
            context.Succeed(requirement);
            return;
        }

        // 3. Resolve current site from cookie
        long siteId;
        try
        {
            var cookie = httpContextAccessor.HttpContext?.Request.Cookies[SiteCookieName];
            if (string.IsNullOrEmpty(cookie) || !long.TryParse(cookie, out siteId))
            {
                context.Fail();
                return;
            }
        }
        catch
        {
            context.Fail();
            return;
        }

        // 4. Check UserSiteAssignment for this user + site + permission
        try
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                context.Fail();
                return;
            }

            var assignment = await querySession.Query<UserSiteAssignment>()
                .FirstOrDefaultAsync(x => x.UserId == userId.Value && x.SiteId == siteId);

            if (assignment is not null &&
                assignment.Permissions.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase))
            {
                context.Succeed(requirement);
                return;
            }
        }
        catch
        {
            // Fall through to fail
        }

        context.Fail();
    }

    private static long? GetUserId(ClaimsPrincipal user)
    {
        // ASP.NET Identity stores the user ID as the NameIdentifier claim.
        // AeroUser uses long IDs (Snowflake), so parse accordingly.
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? user.FindFirst("sub")?.Value;

        if (long.TryParse(sub, out var userId))
            return userId;

        return null;
    }
}
