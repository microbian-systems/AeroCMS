using System.Text;
using Aero.Cms.Core.Models;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.SiteMap;

/// <summary>
/// Maps public sitemap and robots endpoints.
/// </summary>
public static class SitemapApi
{
        /// <summary>
    /// Adds sitemap, culture-specific sitemap, redirect, and robots routes.
    /// </summary>
    /// <param name="app">The route builder that receives the public SEO endpoints.</param>
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

    /// <summary>
    /// Permanently redirects the extensionless sitemap route to <c>/sitemap.xml</c>.
    /// </summary>
    /// <returns>An HTTP permanent redirect result.</returns>
    private static IResult RedirectToSitemapXml()
        => Results.Redirect("/sitemap.xml", permanent: true);

    /// <summary>
    /// Returns the current site's culture sitemap index.
    /// </summary>
    /// <returns>UTF-8 XML on success, or HTTP 500 when generation returns a failure.</returns>
    /// <remarks>Successful production responses receive a five-minute public cache header.</remarks>
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

    /// <summary>
    /// Returns a sitemap URL set for the requested culture.
    /// </summary>
    /// <returns>UTF-8 XML on success, or HTTP 500 for unsupported cultures and generation failures.</returns>
    /// <remarks>Successful production responses receive a five-minute public cache header.</remarks>
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

    /// <summary>
    /// Returns the globally stored robots text or a permissive host-derived default.
    /// </summary>
    /// <returns>A UTF-8 plain-text response.</returns>
    /// <remarks>
    /// The fallback always uses HTTPS and only the request host name; it does not preserve a
    /// non-default port. The <c>SEO.RobotsTxt</c> setting is loaded without site scoping.
    /// </remarks>
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
