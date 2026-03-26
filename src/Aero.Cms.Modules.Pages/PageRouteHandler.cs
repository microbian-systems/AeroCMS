namespace Aero.Cms.Modules.Pages;

using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

public static class PageRouteHandler
{
    /// <summary>
    /// Maps the public page routes for the CMS pages.
    /// </summary>
    public static void MapPageRoutes(this IEndpointRouteBuilder app)
    {
        // Homepage route at /
        app.MapGet("/", GetHomepage)
            .WithName("GetHomepage")
            .WithTags("Pages");

        // Dynamic page route at /{slug}
        app.MapGet("/{slug}", GetPageBySlug)
            .WithName("GetPageBySlug")
            .WithTags("Pages");
    }

    private static async Task<IResult> GetHomepage(
        IPageContentService pageService,
        CancellationToken cancellationToken)
    {
        var result = await pageService.LoadHomepageAsync(cancellationToken);

        if (result is Result<string, PageDocument?>.Failure failure)
        {
            return Results.NotFound(new { error = failure.Error });
        }

        if (result is Result<string, PageDocument?>.Ok { Value: not null } ok)
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
        var normalizedSlug = slug.TrimStart('/');

        var result = await pageService.FindBySlugAsync(normalizedSlug, cancellationToken);

        if (result is Result<string, PageDocument?>.Failure failure)
        {
            return Results.NotFound(new { error = failure.Error });
        }

        if (result is Result<string, PageDocument?>.Ok { Value: not null } ok)
        {
            return Results.Ok(ok.Value);
        }

        return Results.NotFound(new { error = $"Page with slug '{slug}' not found." });
    }
}
