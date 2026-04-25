using Aero.Core.Http;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Web.Infrastructure;

/// <summary>
/// Default implementation of ISiteContext using IHttpContextAccessor.
/// Tries to resolve site and tenant IDs from request headers.
/// </summary>
public sealed class DefaultSiteContext : ISiteContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string SiteIdHeader = "X-Site-Id";
    private const string TenantIdHeader = "X-Tenant-Id";

    public DefaultSiteContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long SiteId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context != null && context.Request.Headers.TryGetValue(SiteIdHeader, out var siteIdStr) && long.TryParse(siteIdStr, out var siteId))
            {
                return siteId;
            }
            return 0;
        }
    }

    public long TenantId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context != null && context.Request.Headers.TryGetValue(TenantIdHeader, out var tenantIdStr) && long.TryParse(tenantIdStr, out var tenantId))
            {
                return tenantId;
            }
            return 0;
        }
    }
}
