namespace Aero.Cms.Modules.Pages.Api;

using System.Threading.Tasks;
using Aero.Core.Railway;
using Aero.Cms.Modules.Pages.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

public static class PagesApi
{
    /// <summary>
    /// Maps the Pages API endpoints.
    /// </summary>
    public static void MapPagesApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/pages/{culture}/{slug}", GetPageByCultureAndSlug)
            .WithName("GetPageByCultureAndSlug")
            .WithTags("Pages");

        app.MapGet("/api/v1/pages", ListPages)
            .WithName("ListPages")
            .WithTags("Pages");
    }

    private static async Task<IResult> GetPageByCultureAndSlug(
        string culture,
        string slug,
        IPageContentService pageService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await pageService.FindBySlugAsync(slug, cancellationToken);

            if (result is Result<string, PageDocument>.Failure failure)
            {
                logger.LogWarning("Page not found for culture={Culture}, slug={Slug}: {Error}",
                    culture, slug, failure.Error);
                return Results.NotFound(new { error = failure.Error });
            }

            if (result is Result<string, PageDocument>.Ok ok)
            {
                return Results.Ok(ok.Value);
            }

            return Results.Json(new { error = "An unexpected error occurred." }, statusCode: 500);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving page for culture={Culture}, slug={Slug}", culture, slug);
            return Results.Json(new { error = "An error occurred processing your request." }, statusCode: 500);
        }
    }

    private static async Task<IResult> ListPages(
        IPageContentService pageService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Query published pages directly using the service's session-like pattern
        // For now, return empty array - the service doesn't have a list method
        // This would need to be extended to query all pages and filter by PublicationState
        return Results.Ok(Array.Empty<PageSummary>());
    }
}

/// <summary>
/// Summary information for a page (used in list responses).
/// </summary>
/// <param name="Id">The page ID.</param>
/// <param name="Slug">The page slug.</param>
/// <param name="Title">The page title.</param>
/// <param name="Summary">The page summary.</param>
/// <param name="PublishedOn">When the page was published.</param>
public record PageSummary(long Id, string Slug, string Title, string? Summary, DateTimeOffset? PublishedOn);
