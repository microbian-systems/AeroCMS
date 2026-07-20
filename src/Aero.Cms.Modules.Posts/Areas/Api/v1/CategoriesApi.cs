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
/// Maps the category administration HTTP surface onto the category actor and Sable count queries.
/// </summary>
/// <remarks>
/// The route group requires an authenticated principal. Site-specific permission policies are
/// applied in a later hardening phase.
/// </remarks>
public static class CategoriesApi
{
    /// <summary>
    /// Maps category list, detail, create, update, and delete endpoints.
    /// </summary>
    /// <param name="app">The route builder to extend.</param>
public static void MapCategoriesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/categories")
            .WithTags("Admin - Categories")
            .RequireAuthorization();

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

    /// <summary>
    /// Lists actor categories filtered to the current site and attaches per-category post counts.
    /// </summary>
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

    /// <summary>
    /// Returns category detail only when the loaded actor model belongs to the current site.
    /// </summary>
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

    /// <summary>
    /// Validates a site-stamped actor request, persists it through the actor, and returns its current post count.
    /// </summary>
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

    /// <summary>
    /// Validates an update and verifies current-site ownership before invoking the actor mutation.
    /// </summary>
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

    /// <summary>
    /// Rejects current-site categories referenced by posts, then delegates deletion by identifier.
    /// </summary>
    /// <remarks>
    /// The preflight count is site-scoped, but this handler does not independently load the category
    /// to verify ownership before the actor's identifier-only delete.
    /// </remarks>
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

    /// <summary>
    /// Counts in-memory category membership after loading all posts for one site.
    /// </summary>
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

    /// <summary>
    /// Adapts the batched category counter to one identifier.
    /// </summary>
    private static Task<int> CountCategoryContentAsync(
        IQuerySession query,
        long siteId,
        long categoryId,
        CancellationToken cancellationToken)
        => GetContentCountsAsync(query, [categoryId], siteId, cancellationToken)
            .ContinueWith(task => task.Result.GetValueOrDefault(categoryId), cancellationToken);

    /// <summary>
    /// Projects an actor model into the category list contract.
    /// </summary>
    private static CategorySummary ToSummary(CategoryViewModel vm, int count)
        => new(vm.Id, vm.Name ?? string.Empty, vm.Slug ?? string.Empty, count, vm.ParentCategoryId);

    /// <summary>
    /// Projects an actor model into the category detail contract with an empty child collection.
    /// </summary>
    private static CategoryDetail ToDetail(CategoryViewModel vm, int count)
        => new(vm.Id, vm.Name ?? string.Empty, vm.Slug ?? string.Empty, vm.Description, count, vm.ParentCategoryId, [], vm.CreatedOn.DateTime);
}
