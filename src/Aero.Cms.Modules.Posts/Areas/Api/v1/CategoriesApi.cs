using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ActorCreateCategoryRequest = Aero.Cms.Abstractions.Requests.CreateCategoryRequest;
using ActorDeleteCategoryRequest = Aero.Cms.Abstractions.Requests.DeleteCategoryRequest;
using ActorUpdateCategoryRequest = Aero.Cms.Abstractions.Requests.UpdateCategoryRequest;
using HttpCreateCategoryRequest = Aero.Cms.Abstractions.Http.Clients.CreateCategoryRequest;
using HttpUpdateCategoryRequest = Aero.Cms.Abstractions.Http.Clients.UpdateCategoryRequest;

namespace Aero.Cms.Modules.Posts.Areas.Api.v1;

/// <summary>
/// Thin admin API for category management.
/// Handles input validation and delegates all logic to <see cref="IAeroCategoryActor"/> (Orleans grain).
/// </summary>
public static class CategoriesApi
{
        /// <summary>
    /// MapCategoriesApi method.
    /// </summary>
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
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        CancellationToken cancellationToken = default)
    {
        var categories = await categoryActor.GetAllAsync(cancellationToken);
        var scoped = categories.Where(x => x.SiteId == siteContext.SiteId).ToList();
        var counts = await GetContentCountsAsync(query, scoped.Select(x => x.Id), siteContext.SiteId, cancellationToken);

        return TypedResults.Ok(scoped
            .Select(x => ToSummary(x, counts.GetValueOrDefault(x.Id)))
            .ToList());
    }

    private static async Task<IResult> GetCategoryById(
        long id,
        [FromServices] IAeroCategoryActor categoryActor,
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(CategoriesApi));
        logger.LogDebug("Getting category {Id}", id);
        var result = await categoryActor.GetByIdAsync(id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(result.error.Message) || result.data.SiteId != siteContext.SiteId)
            return TypedResults.NotFound(result.error);

        var count = await CountCategoryContentAsync(query, siteContext.SiteId, id, cancellationToken);
        return TypedResults.Ok(ToDetail(result.data, count));
    }

    private static async Task<IResult> CreateCategory(
        [FromBody] HttpCreateCategoryRequest request,
        [FromServices] IValidator<ActorCreateCategoryRequest> validator,
        [FromServices] IAeroCategoryActor categoryActor,
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(CategoriesApi));

        var actorRequest = new ActorCreateCategoryRequest(
            siteContext.SiteId,
            request.Name,
            string.IsNullOrWhiteSpace(request.Slug) ? null : request.Slug,
            request.Description);

        var validationResult = await validator.ValidateAsync(actorRequest, cancellationToken);
        if (!validationResult.IsValid)
        {
            logger.LogWarning("CreateCategory validation failed: {Errors}",
                validationResult.Errors.Select(e => e.ErrorMessage));
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        logger.LogDebug("Creating category {Name}", request.Name);
        var result = await categoryActor.CreateAsync(actorRequest, cancellationToken);
        if (!string.IsNullOrWhiteSpace(result.error.Message))
            return TypedResults.BadRequest(result.error);

        var count = await CountCategoryContentAsync(query, siteContext.SiteId, result.data.Id, cancellationToken);
        return TypedResults.Ok(ToDetail(result.data, count));
    }

    private static async Task<IResult> UpdateCategory(
        long id,
        [FromBody] HttpUpdateCategoryRequest request,
        [FromServices] IValidator<ActorUpdateCategoryRequest> validator,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        IAeroCategoryActor categoryActor,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(CategoriesApi));
        logger.LogDebug("Updating category {Id}", id);
        var actorRequest = new ActorUpdateCategoryRequest(
            id,
            request.Name,
            string.IsNullOrWhiteSpace(request.Slug) ? null : request.Slug,
            request.Description);

        var validationResult = await validator.ValidateAsync(actorRequest, cancellationToken);
        if (!validationResult.IsValid)
            return TypedResults.ValidationProblem(validationResult.ToDictionary());

        var existing = await categoryActor.GetByIdAsync(id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existing.error.Message) || existing.data.SiteId != siteContext.SiteId)
            return TypedResults.NotFound(existing.error);

        var result = await categoryActor.UpdateAsync(actorRequest, cancellationToken);
        if (!string.IsNullOrWhiteSpace(result.error.Message))
            return TypedResults.BadRequest(result.error);

        var count = await CountCategoryContentAsync(query, siteContext.SiteId, id, cancellationToken);
        return TypedResults.Ok(ToDetail(result.data, count));
    }

    private static async Task<IResult> DeleteCategory(
        long id,
        [FromServices] IAeroCategoryActor categoryActor,
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(CategoriesApi));
        logger.LogDebug("Deleting category {Id}", id);
        var count = await CountCategoryContentAsync(query, siteContext.SiteId, id, cancellationToken);
        if (count > 0)
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Category is in use",
                Detail = "Move posts to another category before deleting this category.",
                Status = StatusCodes.Status400BadRequest
            });

        var result = await categoryActor.DeleteAsync(new ActorDeleteCategoryRequest(id), cancellationToken);
        return string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.Ok(true)
            : TypedResults.BadRequest(result.error);
    }

    private static async Task<Dictionary<long, int>> GetContentCountsAsync(
        IQuerySession query,
        IEnumerable<long> categoryIds,
        long siteId,
        CancellationToken cancellationToken)
    {
        var ids = categoryIds.Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        var posts = await query.Query<PostDocument>()
            .Where(x => x.SiteId == siteId)
            .ToListAsync(cancellationToken);

        return ids.ToDictionary(id => id, id => posts.Count(post => post.CategoryIds.Contains(id)));
    }

    private static Task<int> CountCategoryContentAsync(
        IQuerySession query,
        long siteId,
        long categoryId,
        CancellationToken cancellationToken)
        => GetContentCountsAsync(query, [categoryId], siteId, cancellationToken)
            .ContinueWith(task => task.Result.GetValueOrDefault(categoryId), cancellationToken);

    private static CategorySummary ToSummary(CategoryViewModel vm, int count)
        => new(vm.Id, vm.Name ?? string.Empty, vm.Slug ?? string.Empty, count, vm.ParentCategoryId);

    private static CategoryDetail ToDetail(CategoryViewModel vm, int count)
        => new(vm.Id, vm.Name ?? string.Empty, vm.Slug ?? string.Empty, vm.Description, count, vm.ParentCategoryId, [], vm.CreatedOn.DateTime);
}
