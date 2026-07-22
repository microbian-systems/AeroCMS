using System.Security.Claims;
using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Modules.Sites;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Mcp;

/// <summary>Re-establishes authentication, authorization, and exact site tenancy for every tool call.</summary>
internal sealed class AeroCmsMcpInvocationContextFactory(
    IHttpContextAccessor httpContextAccessor,
    IAuthorizationService authorizationService,
    ISiteLookupService siteLookupService)
{
    private const string SiteCookieName = "AeroCms.SiteId";

    public async Task<Result<AeroCmsToolExecutionContext>> CreateAsync(CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var principal = httpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return AeroError.UnauthorizedError("Authentication is required.");

        var userId = ReadUserId(principal);
        if (userId <= 0)
            return AeroError.ForbiddenError("A valid user context is required.");

        var requestedSite = httpContext?.Request.Cookies[SiteCookieName];
        if (!long.TryParse(requestedSite, out var siteId) || siteId <= 0)
            return AeroError.ForbiddenError("A valid site context is required.");

        var authorization = await authorizationService.AuthorizeAsync(principal, resource: null, "site:read");
        if (!authorization.Succeeded)
            return AeroError.ForbiddenError("The selected site is not authorized.");

        var sites = await siteLookupService.GetAllAsync(cancellationToken);
        var site = sites.SingleOrDefault(candidate => candidate.Id == siteId);
        if (site is null || site.TenantId <= 0)
            return AeroError.ForbiddenError("A valid tenant context is required.");

        var correlationId = httpContext?.TraceIdentifier;
        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = "mcp";
        if (correlationId.Length > 128)
            correlationId = correlationId[..128];

        return new AeroCmsToolExecutionContext(
            principal,
            userId,
            siteId,
            site.TenantId,
            correlationId);
    }

    private static long ReadUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? principal.FindFirstValue("user_id");
        return long.TryParse(value, out var userId) ? userId : 0;
    }
}
