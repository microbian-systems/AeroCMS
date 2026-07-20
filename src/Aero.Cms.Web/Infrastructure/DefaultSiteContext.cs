using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Http;

namespace Aero.Cms.Web.Infrastructure;

/// <summary>
/// Resolves the current tenant and site from the active HTTP request.
/// </summary>
/// <remarks>
/// For manager and admin API paths, <see cref="SiteId"/> first trusts the parseable
/// <c>AeroCms.SiteId</c> selection cookie. Public paths use the site-resolution feature instead,
/// preventing a manager cookie from changing host-based public routing. This accessor supplies
/// context, not authorization; callers must still enforce access to the selected site.
/// </remarks>
public sealed class DefaultSiteContext : ISiteContext
{
    private static readonly PathString ManagerPathPrefix = "/manager";
    private static readonly PathString AdminApiPathPrefix = "/api/v1/admin";

    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSiteContext"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">Provides access to the current request context.</param>
public DefaultSiteContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the selected manager site or host-resolved public site identifier.
    /// </summary>
    /// <value>Zero when no applicable cookie or resolved site feature exists.</value>
public long SiteId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;

            // Manager/admin requests must use the site picked in the manager UI,
            // because admin API calls are usually made against localhost rather
            // than the public site's hostname.
            if (IsManagerRequest(httpContext))
            {
                var cookie = httpContext?.Request.Cookies["AeroCms.SiteId"];
                if (long.TryParse(cookie, out var siteId))
                    return siteId;
            }

            // Public front-end routes must remain host-based. Otherwise a manager
            // selection cookie can make localhost render another site's content
            // and can cause /oops to miss repeatedly when that site has no error page.
            var slice = httpContext?.Features.Get<IAeroSiteSlice>();
            if (slice is not null)
                return slice.SiteId;

            return 0;
        }
    }

    /// <summary>
    /// Gets the tenant identifier from the host-resolved site feature.
    /// </summary>
    /// <value>
    /// The resolved tenant identifier, or zero when no site feature exists. Manager cookie
    /// selection does not supply a tenant identifier.
    /// </value>
public long TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var slice = httpContext?.Features.Get<IAeroSiteSlice>();
            if (slice is not null)
                return slice.TenantId;

            return 0;
        }
    }

    /// <summary>
    /// Determines whether the current path belongs to the manager or admin API surface.
    /// </summary>
    /// <param name="httpContext">The request context, if one is active.</param>
    /// <returns><see langword="true"/> for <c>/manager</c> or <c>/api/v1/admin</c> path segments.</returns>
    private static bool IsManagerRequest(HttpContext? httpContext)
    {
        if (httpContext is null)
            return false;

        var path = httpContext.Request.Path;
        return path.StartsWithSegments(ManagerPathPrefix, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments(AdminApiPathPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
