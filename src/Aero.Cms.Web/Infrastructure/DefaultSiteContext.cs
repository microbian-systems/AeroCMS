using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Http;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Web.Infrastructure;

/// <summary>
/// Default implementation of ISiteContext using IHttpContextAccessor.
/// Reads the current site from <see cref="IAeroSiteSlice"/> set by
/// <see cref="SiteResolutionMiddleware"/> on <see cref="HttpContext.Features"/>.
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
            return slice?.SiteId ?? 0;
        }
    }

    public long TenantId
    {
        get
        {
            var features = _httpContextAccessor.HttpContext?.Features;
            var slice = features?.Get<IAeroSiteSlice>();
            return slice?.TenantId ?? 0;
        }
    }
}
