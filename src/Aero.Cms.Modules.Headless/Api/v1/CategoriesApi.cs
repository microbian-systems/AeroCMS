using Aero.Cms.Core;
using Aero.Cms.Core.Http.Clients;
using Aero.Cms.Modules.Blog.Models;
using Aero.Core.Railway;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Headless.Api.v1;

/// <summary>
/// Admin API for category management.
/// </summary>
public static class CategoriesApi
{
    /// <summary>
    /// Maps the Categories Admin API endpoints.
    /// </summary>
    public static void MapCategoriesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/categories")
            .WithTags("Admin - Categories");

        group.MapGet("/", GetAllCategories)
            .WithName("GetAllCategories");

        group.MapGet("/details/{id:long}", GetCategoryById)
            .WithName("GetCategoryById");

        group.MapPost("/", CreateCategory)
            .WithName("CreateCategory");

        group.MapPut("/{id:long}", UpdateCategory)
            .WithName("UpdateCategory");

        group.MapDelete("/{id:long}", DeleteCategory)
            .WithName("DeleteCategory");
    }

    private static async Task<IResult> GetAllCategories(
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(CategoriesApi));
        try
        {
            var categories = await session.Query<Category>()
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            var summaries = categories.Select(c => new CategorySummary(
                c.Id,
                c.Name,
                c.Slug,
                0, // TODO: Get content count
                null // TODO: Parent category support if needed
            )).ToList();

            return TypedResults.Ok(summaries);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving all categories");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetCategoryById(
        long id,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(CategoriesApi));
        try
        {
            var category = await session.LoadAsync<Category>(id, cancellationToken);

            if (category is null)
            {
                return TypedResults.NotFound(new { error = $"Category with ID {id} not found." });
            }

            var detail = new CategoryDetail(
                category.Id,
                category.Name,
                category.Slug,
                category.Description,
                0, // TODO: Get content count
                null,
                [],
                category.CreatedOn.DateTime
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving category for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> CreateCategory(
        [FromBody] CreateCategoryRequest request,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(CategoriesApi));
        try
        {
            var category = new Category
            {
                Id = Snowflake.NewId(),
                Name = request.Name,
                Slug = request.Slug,
                Description = request.Description
            };

            session.Store(category);
            await session.SaveChangesAsync(cancellationToken);

            var detail = new CategoryDetail(
                category.Id,
                category.Name,
                category.Slug,
                category.Description,
                0,
                null,
                [],
                category.CreatedOn.DateTime
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating category");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateCategory(
        long id,
        [FromBody] UpdateCategoryRequest request,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(CategoriesApi));
        try
        {
            var category = await session.LoadAsync<Category>(id, cancellationToken);

            if (category is null)
            {
                return TypedResults.NotFound(new { error = $"Category with ID {id} not found." });
            }

            category.Name = request.Name;
            category.Slug = request.Slug;
            category.Description = request.Description;

            session.Store(category);
            await session.SaveChangesAsync(cancellationToken);

            var detail = new CategoryDetail(
                category.Id,
                category.Name,
                category.Slug,
                category.Description,
                0,
                null,
                [],
                category.CreatedOn.DateTime
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating category for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> DeleteCategory(
        long id,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(CategoriesApi));
        try
        {
            var category = await session.LoadAsync<Category>(id, cancellationToken);

            if (category is null)
            {
                return TypedResults.NotFound(new { error = $"Category with ID {id} not found." });
            }

            session.Delete(category);
            await session.SaveChangesAsync(cancellationToken);

            return TypedResults.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting category for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }
}
