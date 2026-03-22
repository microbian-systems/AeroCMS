namespace Aero.Cms.Modules.Pages;

using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

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
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await pageService.LoadHomepageAsync(cancellationToken);

            if (result is Result<string, PageDocument?>.Failure failure)
            {
                logger.LogWarning("Homepage not found: {Error}", failure.Error);
                return Results.NotFound(new { error = failure.Error });
            }

            if (result is Result<string, PageDocument?>.Ok { Value: not null } ok)
            {
                return Results.Ok(ok.Value);
            }

            return Results.NotFound(new { error = "Homepage not found." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving homepage");
            return Results.Json(new { error = "An error occurred processing your request." }, statusCode: 500);
        }
    }

    private static async Task<IResult> GetPageBySlug(
        string slug,
        IPageContentService pageService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Normalize slug - remove leading slash if present for consistency
            var normalizedSlug = slug.TrimStart('/');

            var result = await pageService.FindBySlugAsync(normalizedSlug, cancellationToken);

            if (result is Result<string, PageDocument?>.Failure failure)
            {
                logger.LogWarning("Page not found for slug={Slug}: {Error}", slug, failure.Error);
                return Results.NotFound(new { error = failure.Error });
            }

            if (result is Result<string, PageDocument?>.Ok { Value: not null } ok)
            {
                return Results.Ok(ok.Value);
            }

            return Results.NotFound(new { error = $"Page with slug '{slug}' not found." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving page for slug={Slug}", slug);
            return Results.Json(new { error = "An error occurred processing your request." }, statusCode: 500);
        }
    }
}
