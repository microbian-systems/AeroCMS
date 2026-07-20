using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Http;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Web.Bootstrap.Infrastructure;

/// <summary>
/// Resolves the current site and tenant identifiers from the active HTTP request.
/// </summary>
/// <remarks>
/// Values are read on each access; this type does not retain request state.
/// <para>
/// For paths under <c>/manager</c> and <c>/api/v1/admin</c>, <see cref="SiteId"/> accepts any
/// <c>AeroCms.SiteId</c> request-cookie value that parses as a <see cref="long"/>. The cookie is checked
/// before <see cref="IAeroSiteSlice"/> and is not checked here for site existence, authentication,
/// authorization, tenant membership, or equality with the slice. A slice is not required for that
/// cookie value to be returned.
/// </para>
/// <para>
/// <see cref="TenantId"/> is resolved independently from <see cref="IAeroSiteSlice"/> and falls back to
/// <c>0</c>. Consequently, the site and tenant values can originate from different sources or contain a
/// cookie-derived site with no tenant slice. This context exposes request selection state; consumers must
/// not treat the pair as a validated tenancy or authorization boundary.
/// </para>
/// </remarks>
public sealed class DefaultSiteContext(IHttpContextAccessor httpContextAccessor) : ISiteContext
{
    private static readonly PathString ManagerPathPrefix = "/manager";
    private static readonly PathString AdminApiPathPrefix = "/api/v1/admin";

    /// <summary>
    /// Gets the current site identifier.
    /// </summary>
    /// <value>
    /// For Manager and admin API requests, the raw <c>AeroCms.SiteId</c> cookie when it can be parsed as a
    /// <see cref="long"/>; otherwise, the site-slice identifier or <c>0</c> when no slice is available.
    /// A returned cookie value has not been checked for existence, authorization, tenant membership, or
    /// agreement with the slice.
    /// </value>
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
    /// Gets the tenant identifier from the current request's site slice.
    /// </summary>
    /// <value>
    /// The site-slice tenant identifier, or <c>0</c> when no slice is available. This value is resolved
    /// independently of a cookie-derived <see cref="SiteId"/>.
    /// </value>
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
