using System.Security.Claims;
using Aero.Cms.Core.Entities;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Authorizes a named permission against the site selected in the manager cookie.
/// </summary>
/// <param name="httpContextAccessor">Provides the request cookie containing the selected site.</param>
/// <param name="querySession">Loads the matching user-site assignment.</param>
/// <remarks>
/// Authentication is required. Administrators identified by the <c>Admin</c> role or an exact
/// <c>is_admin=true</c> claim bypass assignment lookup. Other users must expose a numeric identity
/// claim and have a matching <see cref="UserSiteAssignment"/>. Cookie, claim, and database failures
/// fail closed without escaping the handler.
/// </remarks>
public sealed class SitePermissionHandler(
    IHttpContextAccessor httpContextAccessor,
    IQuerySession querySession) : AuthorizationHandler<SitePermissionRequirement>
{
    /// <summary>
    /// Names the HTTP-only manager cookie containing the selected site identifier.
    /// </summary>
    private const string SiteCookieName = "AeroCms.SiteId";

    /// <summary>
    /// Evaluates the current principal and selected site against the required permission.
    /// </summary>
    /// <param name="context">The authorization context to fail or mark successful.</param>
    /// <param name="requirement">The site permission requested by the active policy.</param>
    /// <returns>A task that completes after authentication, cookie, and assignment checks finish.</returns>
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

    /// <summary>
    /// Extracts the Snowflake user identifier from the standard name-identifier or subject claim.
    /// </summary>
    /// <param name="user">The authenticated principal.</param>
    /// <returns>The parsed identifier, or <see langword="null"/> when neither claim is numeric.</returns>
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
