using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Http;

namespace Aero.Cms.Web.Infrastructure;

/// <summary>
/// Default implementation of ISiteContext using IHttpContextAccessor.
///
/// Resolution order (first match wins):
/// 1. <c>AeroCms.SiteId</c> cookie for manager/admin requests — set by explicit
///    user selection in the manager.
/// 2. <see cref="IAeroSiteSlice"/> from <see cref="HttpContext.Features"/> — set by
///    <see cref="SiteResolutionMiddleware"/> for public front-end routes.
/// 3. Returns 0 if neither is available.
/// </summary>
public sealed class DefaultSiteContext : ISiteContext
{
    private static readonly PathString ManagerPathPrefix = "/manager";
    private static readonly PathString AdminApiPathPrefix = "/api/v1/admin";

    private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSiteContext"/> class.
    /// </summary>
public DefaultSiteContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
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
    /// Gets or sets the Tenant Id.
    /// </summary>
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

    private static bool IsManagerRequest(HttpContext? httpContext)
    {
        if (httpContext is null)
            return false;

        var path = httpContext.Request.Path;
        return path.StartsWithSegments(ManagerPathPrefix, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments(AdminApiPathPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
