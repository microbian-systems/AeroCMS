using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Http;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Web.Bootstrap.Infrastructure;

/// <summary>
/// Default server-side <see cref="ISiteContext"/> implementation.
/// </summary>
public sealed class DefaultSiteContext(IHttpContextAccessor httpContextAccessor) : ISiteContext
{
    private static readonly PathString ManagerPathPrefix = "/manager";
    private static readonly PathString AdminApiPathPrefix = "/api/v1/admin";

        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;

            if (IsManagerRequest(httpContext))
            {
                var cookie = httpContext?.Request.Cookies["AeroCms.SiteId"];
                if (long.TryParse(cookie, out var siteId))
                    return siteId;
            }

            var slice = httpContext?.Features.Get<IAeroSiteSlice>();
            return slice?.SiteId ?? 0;
        }
    }

        /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
public long TenantId
    {
        get
        {
            var slice = httpContextAccessor.HttpContext?.Features.Get<IAeroSiteSlice>();
            return slice?.TenantId ?? 0;
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
