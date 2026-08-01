using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Modules.OutputCache.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class CmsOutputCachePolicyThemeVariationTests
{
    [Test]
    public async Task Cache_key_varies_by_resolved_site_and_exact_theme_selection()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("example.test");
        httpContext.Request.Path = "/about";
        httpContext.Features.Set<IAeroSiteSlice>(new AeroSiteSlice
        {
            SiteId = 42,
            TenantId = 7,
            ThemeId = "ocean",
            ThemeVersion = "2.1.0",
            ThemeRevision = 9
        });
        var context = new OutputCacheContext { HttpContext = httpContext };

        await ((IOutputCachePolicy)CmsOutputCachePolicy.Instance)
            .CacheRequestAsync(context, CancellationToken.None);

        await Assert.That(context.CacheVaryByRules.VaryByValues["site-id"]).IsEqualTo("42");
        await Assert.That(context.CacheVaryByRules.VaryByValues["theme-id"]).IsEqualTo("ocean");
        await Assert.That(context.CacheVaryByRules.VaryByValues["theme-version"]).IsEqualTo("2.1.0");
        await Assert.That(context.CacheVaryByRules.VaryByValues["theme-revision"]).IsEqualTo("9");
    }
}
