using Aero.Cms.Core.Http.Clients;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Headless.Api.v1;

/// <summary>
/// Admin API for page content management.
/// </summary>
public static class PagesApi
{
    /// <summary>
    /// Maps the Pages Admin API endpoints.
    /// </summary>
    public static void MapPagesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/pages")
            .WithTags("Admin - Pages");

        group.MapGet("/", ListPages)
            .WithName("ListPages");

        group.MapGet("/{id:long}", GetPageById)
            .WithName("GetPageById");

        group.MapGet("/slug/{*slug}", GetPageBySlug)
            .WithName("GetPageBySlug");

        group.MapPost("/", CreatePage)
            .WithName("CreatePage");

        group.MapPut("/{id:long}", UpdatePage)
            .WithName("UpdatePage");

        group.MapDelete("/{id:long}", DeletePage)
            .WithName("DeletePage");
    }

    private static async Task<IResult> ListPages(
        [FromServices] IPageContentService pageService,
        [FromServices] ILoggerFactory loggerFactory,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pageService.GetAllPagesAsync(skip, take, search, cancellationToken);
            if (result is Result<string, (IReadOnlyList<PageDocument> Items, long TotalCount)>.Ok ok)
            {
                var summary = ok.Value.Items.Select(p => new PageSummary(
                    p.Id, 
                    p.Title, 
                    p.Slug, 
                    p.CreatedOn.DateTime, 
                    p.PublishedOn?.DateTime, 
                    p.Summary)).ToList();

                return TypedResults.Ok(new PagedResult<PageSummary>(summary, ok.Value.TotalCount, skip, take));
            }

            return TypedResults.Problem("Failed to list pages");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing pages");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetPageById(
        long id,
        [FromServices] IPageContentService pageService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pageService.LoadAsync(id, cancellationToken);
            if (result is Result<string, PageDocument?>.Ok { Value: not null } ok)
            {
                return TypedResults.Ok(MapToDetail(ok.Value));
            }

            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving page for id={Id}", id);
            return TypedResults.NotFound();
        }
    }

    private static async Task<IResult> GetPageBySlug(
        string slug,
        [FromServices] IPageContentService pageService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pageService.FindBySlugAsync(slug, cancellationToken);
            if (result is Result<string, PageDocument?>.Ok { Value: not null } ok)
            {
                return TypedResults.Ok(MapToDetail(ok.Value));
            }

            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving page for slug={Slug}", slug);
            return TypedResults.NotFound();
        }
    }

    private static async Task<IResult> CreatePage(
        [FromBody] Aero.Cms.Core.Http.Clients.CreatePageRequest request,
        [FromServices] IPageContentService pageService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var moduleRequest = new Aero.Cms.Modules.Pages.Requests.CreatePageRequest(
                request.Title,
                request.Slug,
                request.Summary,
                request.SeoTitle,
                request.SeoDescription,
                request.PublicationState,
                request.LayoutRegions,
                request.ShowInNavMenu,
                request.EditorBlocks
            );

            var result = await pageService.CreateAsync(moduleRequest, cancellationToken);
            if (result is Result<string, PageDocument>.Ok ok)
            {
                return TypedResults.Created($"/api/v1/admin/pages/{ok.Value.Id}", MapToDetail(ok.Value));
            }

            if (result is Result<string, PageDocument>.Failure failure)
            {
                logger.LogWarning("Failed to create page. Error: {Error}. Request: {@Request}", failure.Error, request);
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to create page",
                    Detail = failure.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return TypedResults.Problem("An unexpected error occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating page. Request: {@Request}", request);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdatePage(
        long id,
        [FromBody] Aero.Cms.Core.Http.Clients.UpdatePageRequest request,
        [FromServices] IPageContentService pageService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var moduleRequest = new Aero.Cms.Modules.Pages.Requests.UpdatePageRequest(
                id,
                request.Title,
                request.Slug,
                request.Summary,
                request.SeoTitle,
                request.SeoDescription,
                request.PublicationState,
                request.LayoutRegions,
                request.ShowInNavMenu,
                request.EditorBlocks
            );

            var result = await pageService.UpdateAsync(id, moduleRequest, cancellationToken);
            if (result is Result<string, PageDocument>.Ok ok)
            {
                return TypedResults.Ok(MapToDetail(ok.Value));
            }

            if (result is Result<string, PageDocument>.Failure failure)
            {
                logger.LogWarning("Failed to update page {Id}. Error: {Error}. Request: {@Request}", id, failure.Error, request);
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to update page",
                    Detail = failure.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return TypedResults.Problem("An unexpected error occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating page for id={Id}. Request: {@Request}", id, request);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> DeletePage(
        long id,
        [FromServices] IPageContentService pageService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pageService.DeleteAsync(id, cancellationToken);
            if (result is Result<string, bool>.Ok { Value: true })
            {
                return TypedResults.Ok(true);
            }

            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting page for id={Id}", id);
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to delete page",
                Detail = ex.Message
            });
        }
    }

    private static PageDetail MapToDetail(PageDocument p)
    {
        return new PageDetail(
            p.Id,
            p.Title,
            p.Slug,
            p.Summary,
            p.SeoTitle,
            p.SeoDescription,
            p.CreatedOn.DateTime,
            p.ModifiedOn.Value.DateTime,
            p.PublishedOn?.DateTime,
            p.PublicationState,
            p.Blocks.Count,
            p.ShowInNavMenu,
            p.Blocks
        );
    }
}
