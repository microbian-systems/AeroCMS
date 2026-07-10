using System.Text;
using Aero.Cms.Core.Models;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.SiteMap;

public static class SitemapApi
{
    public static void MapSitemapApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sitemap", RedirectToSitemapXml)
            .WithName("GetSitemapRedirect")
            .WithTags("SEO");

        app.MapGet("/sitemap.xml", GetSitemap)
            .WithName("GetSitemap")
            .WithTags("SEO");

        app.MapGet("/sitemap-{culture}.xml", GetCultureSitemap)
            .WithName("GetCultureSitemap")
            .WithTags("SEO");

        app.MapGet("/robots.txt", GetRobotsTxt)
            .WithName("GetRobotsTxt")
            .WithTags("SEO");
    }

    private static IResult RedirectToSitemapXml()
        => Results.Redirect("/sitemap.xml", permanent: true);

    private static async Task<IResult> GetSitemap(
        HttpContext httpContext,
        ISiteMapService sitemapService,
        IHostEnvironment environment,
        CancellationToken ct)
    {
        var result = await sitemapService.BuildSitemapIndexAsync(ct);
        if (result is Result<string, AeroError>.Ok ok)
        {
            if (environment.IsProduction())
            {
                httpContext.Response.Headers.CacheControl = "public,max-age=300";
            }

            return Results.Content(ok.Value, "application/xml", Encoding.UTF8);
        }

        return Results.Problem("Failed to generate sitemap");
    }

    private static async Task<IResult> GetCultureSitemap(
        string culture,
        HttpContext httpContext,
        ISiteMapService sitemapService,
        IHostEnvironment environment,
        CancellationToken ct)
    {
        var result = await sitemapService.BuildSitemapAsync(culture, ct);
        if (result is Result<string, AeroError>.Ok ok)
        {
            if (environment.IsProduction())
            {
                httpContext.Response.Headers.CacheControl = "public,max-age=300";
            }

            return Results.Content(ok.Value, "application/xml", Encoding.UTF8);
        }

        return Results.Problem("Failed to generate sitemap");
    }

    private static async Task<IResult> GetRobotsTxt(
        HttpContext httpContext,
        IQuerySession session,
        CancellationToken ct)
    {
        var setting = await session.LoadAsync<Setting>("SEO.RobotsTxt", ct);
        var content = setting?.Value;

        if (string.IsNullOrWhiteSpace(content))
        {
            var host = httpContext.Request.Host.Host;
            content = $"User-agent: *\nAllow: /\nSitemap: https://{host}/sitemap.xml\n";
        }

        return Results.Content(content, "text/plain", Encoding.UTF8);
    }
}
