using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Http;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Web.Infrastructure;

/// <summary>
/// Default implementation of ISiteContext using IHttpContextAccessor.
/// Reads the current site from <see cref="IAeroSiteSlice"/> set by
/// <see cref="SiteResolutionMiddleware"/> on <see cref="HttpContext.Features"/>.
///
/// For <c>/manager/*</c> routes where the middleware is skipped, falls back
/// to reading the <c>AeroCms.SiteId</c> cookie set by explicit user selection.
/// </summary>
public sealed class DefaultSiteContext : ISiteContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DefaultSiteContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long SiteId
    {
        get
        {
            var features = _httpContextAccessor.HttpContext?.Features;
            var slice = features?.Get<IAeroSiteSlice>();
            if (slice is not null)
                return slice.SiteId;

            // Fallback for /manager/* routes: read from the user's site selection cookie
            var cookie = _httpContextAccessor.HttpContext?.Request.Cookies["AeroCms.SiteId"];
            if (long.TryParse(cookie, out var siteId))
                return siteId;

            return 0;
        }
    }

    public long TenantId
    {
        get
        {
            var features = _httpContextAccessor.HttpContext?.Features;
            var slice = features?.Get<IAeroSiteSlice>();
            if (slice is not null)
                return slice.TenantId;

            // Fallback — no tenant cookie at this point; this field is informational
            return 0;
        }
    }
}
