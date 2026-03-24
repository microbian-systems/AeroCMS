using Aero.Cms.Core.Http.Clients;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Aero.Cms.Web.Core;
using Aero.Cms.Core;

namespace Aero.Cms.Modules.Headless.Api.v1;

public static class PagesApi
{
    /// <summary>
    /// Maps the Pages Admin API endpoints.
    /// </summary>
    public static void MapPagesApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/pages", ListPages)
            .WithName("ListPages")
            .WithTags("Admin - Pages");

        app.MapGet("/api/v1/admin/pages/{id:long}", GetPageById)
            .WithName("GetPageById")
            .WithTags("Admin - Pages");

        app.MapGet("/api/v1/admin/pages/by-slug/{slug}", GetPageBySlug)
            .WithName("GetPageBySlug")
            .WithTags("Admin - Pages");

        app.MapPost("/api/v1/admin/pages", CreatePage)
            .WithName("CreatePage")
            .WithTags("Admin - Pages");

        app.MapPut("/api/v1/admin/pages/{id:long}", UpdatePage)
            .WithName("UpdatePage")
            .WithTags("Admin - Pages");

        app.MapDelete("/api/v1/admin/pages/{id:long}", DeletePage)
            .WithName("DeletePage")
            .WithTags("Admin - Pages");
    }

    private static async Task<IResult> ListPages(
        IPageContentService pageService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pageService.GetAllPagesAsync(cancellationToken);

            if (result is Result<string, IReadOnlyList<PageDocument>>.Failure failure)
            {
                logger.LogWarning("Failed to retrieve pages: {Error}", failure.Error);
                return TypedResults.NotFound();
            }

            if (result is Result<string, IReadOnlyList<PageDocument>>.Ok ok)
            {
                return TypedResults.Ok(ok.Value);
            }

            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving pages");
            return TypedResults.NotFound();
        }
    }

    private static async Task<IResult> GetPageById(
        long id,
        IPageContentService pageService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pageService.LoadAsync(id, cancellationToken);

            if (result is Result<string, PageDocument?>.Failure failure)
            {
                logger.LogWarning("Page not found for id={Id}: {Error}", id, failure.Error);
                return TypedResults.NotFound();
            }

            if (result is Result<string, PageDocument?>.Ok { Value: not null } ok)
            {
                return TypedResults.Ok(ok.Value);
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
        IPageContentService pageService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pageService.FindBySlugAsync(slug, cancellationToken);

            if (result is Result<string, PageDocument?>.Failure failure)
            {
                logger.LogWarning("Page not found for slug={Slug}: {Error}", slug, failure.Error);
                return TypedResults.NotFound();
            }

            if (result is Result<string, PageDocument?>.Ok { Value: not null } ok)
            {
                return TypedResults.Ok(ok.Value);
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
        [FromBody] CreatePageRequest request,
        IPageContentService pageService,
        IDocumentSession session,
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var page = new PageDocument
            {
                Id = Snowflake.NewId(),
                Title = request.Title,
                Slug = request.Slug,
                Summary = request.Summary,
                SeoTitle = request.SeoTitle,
                SeoDescription = request.SeoDescription,
                PublicationState = request.PublicationState
            };

            var result = await pageService.SaveAsync(page, cancellationToken);

            if (result is Result<string, PageDocument>.Failure failure)
            {
                logger.LogWarning("Failed to create page: {Error}", failure.Error);
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to create page",
                    Detail = failure.Error
                });
            }

            if (result is Result<string, PageDocument>.Ok ok)
            {
                logger.LogInformation("Created page id={Id}, slug={Slug}", ok.Value.Id, ok.Value.Slug);
                return TypedResults.Created($"/api/v1/admin/pages/{ok.Value.Id}", ok.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to create page",
                Detail = "An unexpected error occurred"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating page");
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to create page",
                Detail = ex.Message
            });
        }
    }

    private static async Task<IResult> UpdatePage(
        long id,
        [FromBody] UpdatePageRequest request,
        IPageContentService pageService,
        IDocumentSession session,
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var loadResult = await pageService.LoadAsync(id, cancellationToken);

            if (loadResult is Result<string, PageDocument?>.Failure failure)
            {
                logger.LogWarning("Failed to load page id={Id}: {Error}", id, failure.Error);
                return TypedResults.NotFound(new ProblemDetails
                {
                    Title = "Page not found",
                    Detail = failure.Error
                });
            }

            if (loadResult is Result<string, PageDocument?>.Ok { Value: null })
            {
                return TypedResults.NotFound(new ProblemDetails
                {
                    Title = "Page not found",
                    Detail = $"Page with id '{id}' not found"
                });
            }

            if (loadResult is not Result<string, PageDocument?>.Ok { Value: not null } ok)
            {
                return TypedResults.NotFound(new ProblemDetails
                {
                    Title = "Page not found",
                    Detail = $"Page with id '{id}' not found"
                });
            }

            var existingPage = ok.Value;

            existingPage.Title = request.Title;
            existingPage.Slug = request.Slug;
            existingPage.Summary = request.Summary;
            existingPage.SeoTitle = request.SeoTitle;
            existingPage.SeoDescription = request.SeoDescription;
            existingPage.PublicationState = request.PublicationState;

            var saveResult = await pageService.SaveAsync(existingPage, cancellationToken);

            if (saveResult is Result<string, PageDocument>.Failure saveFailure)
            {
                logger.LogWarning("Failed to update page id={Id}: {Error}", id, saveFailure.Error);
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to update page",
                    Detail = saveFailure.Error
                });
            }

            if (saveResult is Result<string, PageDocument>.Ok saveOk)
            {
                logger.LogInformation("Updated page id={Id}, slug={Slug}", saveOk.Value.Id, saveOk.Value.Slug);
                return TypedResults.Ok(saveOk.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to update page",
                Detail = "An unexpected error occurred"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating page id={Id}", id);
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to update page",
                Detail = ex.Message
            });
        }
    }

    private static async Task<IResult> DeletePage(
        long id,
        IDocumentSession session,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var page = await session.LoadAsync<PageDocument>(id, cancellationToken);

            if (page is null)
            {
                return TypedResults.NotFound(new ProblemDetails
                {
                    Title = "Page not found",
                    Detail = $"Page with id '{id}' not found"
                });
            }

            session.Delete(page);
            await session.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Deleted page id={Id}", id);
            return TypedResults.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting page id={Id}", id);
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to delete page",
                Detail = ex.Message
            });
        }
    }
}
