namespace Aero.Cms.Modules.Pages;

using Aero.Cms.Core.Entities;
using Aero.Cms.Shared.Localization;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Maps headless page-document endpoints for homepage and catch-all slug lookups.
/// </summary>
/// <remarks>
/// These routes return page documents. Public HTML rendering is provided separately
/// by the Pages Razor Page.
/// </remarks>
public static class PageRouteHandler
{
    /// <summary>
    /// Maps <c>/</c> and <c>/{*slug}</c> GET endpoints.
    /// </summary>
    /// <param name="app">The endpoint route builder to extend.</param>
    /// <remarks>
    /// The catch-all handler removes a leading culture segment before performing the
    /// site-scoped slug lookup. Service failures and missing pages are both returned
    /// as HTTP 404 responses by these handlers.
    /// </remarks>
    public static void MapPageRoutes(this IEndpointRouteBuilder app)
    {
        // Homepage route at /
        app.MapGet("/", GetHomepage)
            .WithName("GetHomepage")
            .WithTags("Pages");

        // Dynamic page route at /{*slug} — catch-all for hierarchical paths
        // NOTE: Public HTML rendering is handled by the Razor Page at Areas/Cms/Pages/Page.cshtml
        // which also uses a catch-all (/{**slug}). This Minimal API is for headless/programmatic access.
        app.MapGet("/{*slug}", GetPageBySlug)
            .WithName("GetPageBySlug")
            .WithTags("Pages");
    }

    private static async Task<IResult> GetHomepage(
        IPageContentService pageService,
        CancellationToken cancellationToken)
    {
        var result = await pageService.LoadHomepageAsync(cancellationToken);

        if (result is Result<PageDocument?, AeroError>.Failure failure)
        {
            return Results.NotFound(new { error = failure.Error });
        }

        if (result is Result<PageDocument?, AeroError>.Ok { Value: not null } ok)
        {
            return Results.Ok(ok.Value);
        }

        return Results.NotFound(new { error = "Homepage not found." });
    }

    private static async Task<IResult> GetPageBySlug(
        string slug,
        IPageContentService pageService,
        CancellationToken cancellationToken)
    {
        // Normalize slug - remove leading slash if present for consistency
        var normalizedSlug = AeroCultureRoute.StripLeadingCulture(slug);

        var result = await pageService.FindBySlugAsync(normalizedSlug, cancellationToken);

        if (result is Result<PageDocument?, AeroError>.Failure failure)
        {
            return Results.NotFound(new { error = failure.Error });
        }

        if (result is Result<PageDocument?, AeroError>.Ok { Value: not null } ok)
        {
            return Results.Ok(ok.Value);
        }

        return Results.NotFound(new { error = $"Page with slug '{slug}' not found." });
    }
}
