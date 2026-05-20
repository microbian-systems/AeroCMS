using Aero.Cms.Abstractions.Actors;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Blog.Areas.Api.v1;

/// <summary>
/// Thin admin API for category management.
/// Handles input validation and delegates all logic to <see cref="IAeroCategoryActor"/> (Orleans grain).
/// </summary>
public static class CategoriesApi
{
    public static void MapCategoriesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/categories")
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
        [FromServices] IAeroCategoryActor categoryActor,
        CancellationToken cancellationToken = default)
    {
        var categories = await categoryActor.GetAllAsync(cancellationToken);
        return TypedResults.Ok(categories);
    }

    private static async Task<IResult> GetCategoryById(
        long id,
        [FromServices] IAeroCategoryActor categoryActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(CategoriesApi));
        logger.LogDebug("Getting category {Id}", id);
        var result = await categoryActor.GetByIdAsync(id, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> CreateCategory(
        [FromBody] CreateCategoryRequest request,
        [FromServices] IValidator<CreateCategoryRequest> validator,
        [FromServices] IAeroCategoryActor categoryActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(CategoriesApi));

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            logger.LogWarning("CreateCategory validation failed: {Errors}",
                validationResult.Errors.Select(e => e.ErrorMessage));
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        logger.LogDebug("Creating category {Name}", request.Name);
        var result = await categoryActor.CreateAsync(request, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> UpdateCategory(
        long id,
        [FromBody] UpdateCategoryRequest request,
        [FromServices] ILoggerFactory loggerFactory,
        IAeroCategoryActor categoryActor,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(CategoriesApi));
        logger.LogDebug("Updating category {Id}", id);
        var result = await categoryActor.UpdateAsync(request with { Id = id }, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> DeleteCategory(
        long id,
        [FromServices] IAeroCategoryActor categoryActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(CategoriesApi));
        logger.LogDebug("Deleting category {Id}", id);
        var result = await categoryActor.DeleteAsync(new DeleteCategoryRequest(id), cancellationToken);
        return TypedResults.Ok(result);
    }
}
