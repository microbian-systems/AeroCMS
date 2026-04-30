using System.Text;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.SiteMap;

public static class SitemapApi
{
    public static void MapSitemapApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sitemap", GetSitemap)
            .WithName("GetSitemapRedirect") // todo - redirect /sitemap to /sitemap.xml
            .WithTags("SEO");

        app.MapGet("/sitemap.xml", GetSitemap)
            .WithName("GetSitemap")
            .WithTags("SEO");
    }

    private static async Task<IResult> GetSitemap(
        ISiteMapService sitemapService,
        CancellationToken ct)
    {
        var result = await sitemapService.BuildSitemapAsync(ct);
        if (result is Result<string, AeroError>.Ok ok)
            return Results.Content(ok.Value, "application/xml", Encoding.UTF8);
        return Results.Problem("Failed to generate sitemap");
    }
}
