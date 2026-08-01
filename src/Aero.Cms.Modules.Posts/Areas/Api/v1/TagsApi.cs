using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ActorCreateTagRequest = Aero.Cms.Abstractions.Requests.CreateTagRequest;
using ActorDeleteTagRequest = Aero.Cms.Abstractions.Requests.DeleteTagRequest;
using ActorUpdateTagRequest = Aero.Cms.Abstractions.Requests.UpdateTagRequest;
using HttpCreateTagRequest = Aero.Cms.Abstractions.Http.Clients.CreateTagRequest;
using HttpUpdateTagRequest = Aero.Cms.Abstractions.Http.Clients.UpdateTagRequest;

namespace Aero.Cms.Modules.Posts.Areas.Api.v1;

/// <summary>
/// Maps the tag administration HTTP surface onto the tag actor and Sable count queries.
/// </summary>
/// <remarks>
/// The route group requires an authenticated principal. Site-specific permission policies are
/// applied in a later hardening phase.
/// </remarks>
public static class TagsApi
{
    /// <summary>
    /// Maps tag list, detail, create, update, and delete endpoints.
    /// </summary>
    /// <param name="app">The route builder to extend.</param>
public static void MapTagsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/tags")
            .WithTags("Admin - Tags")
            .RequireAuthorization();

        group.MapGet("/", GetAllTags)
            .WithName("GetAllTags");

        group.MapGet("/details/{id:long}", GetTagById)
            .WithName("GetTagById");

        group.MapPost("/", CreateTag)
            .WithName("CreateTag");

        group.MapPut("/{id:long}", UpdateTag)
            .WithName("UpdateTag");

        group.MapDelete("/{id:long}", DeleteTag)
            .WithName("DeleteTag");
    }

    /// <summary>
    /// Lists actor tags filtered to the current site and attaches per-tag post counts.
    /// </summary>
    private static async Task<IResult> GetAllTags(
        [FromServices] IAeroTagActor tagActor,
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        CancellationToken cancellationToken = default)
    {
        var tags = await tagActor.GetAllAsync(cancellationToken);
        var scoped = tags.Where(x => x.SiteId == siteContext.SiteId).ToList();
        var counts = await GetContentCountsAsync(query, scoped.Select(x => x.Id), siteContext.SiteId, cancellationToken);

        return TypedResults.Ok(scoped
            .Select(x => ToSummary(x, counts.GetValueOrDefault(x.Id)))
            .ToList());
    }

    /// <summary>
    /// Returns tag detail only when the loaded actor model belongs to the current site.
    /// </summary>
    private static async Task<IResult> GetTagById(
        long id,
        [FromServices] IAeroTagActor tagActor,
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(TagsApi));
        logger.LogDebug("Getting tag {Id}", id);
        var result = await tagActor.GetByIdAsync(id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(result.error.Message) || result.data.SiteId != siteContext.SiteId)
            return TypedResults.NotFound(result.error);

        var count = await CountTagContentAsync(query, siteContext.SiteId, id, cancellationToken);
        return TypedResults.Ok(ToDetail(result.data, count));
    }

    /// <summary>
    /// Validates a site-stamped actor request, persists it through the actor, and returns its current post count.
    /// </summary>
    private static async Task<IResult> CreateTag(
        [FromBody] HttpCreateTagRequest request,
        [FromServices] IValidator<ActorCreateTagRequest> validator,
        [FromServices] IAeroTagActor tagActor,
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(TagsApi));

        var actorRequest = new ActorCreateTagRequest(
            siteContext.SiteId,
            request.Name,
            string.IsNullOrWhiteSpace(request.Slug) ? null : request.Slug,
            request.Description);

        var validationResult = await validator.ValidateAsync(actorRequest, cancellationToken);
        if (!validationResult.IsValid)
        {
            logger.LogWarning("CreateTag validation failed: {Errors}",
                validationResult.Errors.Select(e => e.ErrorMessage));
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        logger.LogDebug("Creating tag {Name}", request.Name);
        var result = await tagActor.CreateAsync(actorRequest, cancellationToken);
        if (!string.IsNullOrWhiteSpace(result.error.Message))
            return TypedResults.BadRequest(result.error);

        var count = await CountTagContentAsync(query, siteContext.SiteId, result.data.Id, cancellationToken);
        return TypedResults.Ok(ToDetail(result.data, count));
    }

    /// <summary>
    /// Validates an update and verifies current-site ownership before invoking the actor mutation.
    /// </summary>
    private static async Task<IResult> UpdateTag(
        long id,
        [FromBody] HttpUpdateTagRequest request,
        [FromServices] IValidator<ActorUpdateTagRequest> validator,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        IAeroTagActor tagActor,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(TagsApi));
        logger.LogDebug("Updating tag {Id}", id);
        var actorRequest = new ActorUpdateTagRequest(
            id,
            request.Name,
            string.IsNullOrWhiteSpace(request.Slug) ? null : request.Slug,
            request.Description);

        var validationResult = await validator.ValidateAsync(actorRequest, cancellationToken);
        if (!validationResult.IsValid)
            return TypedResults.ValidationProblem(validationResult.ToDictionary());

        var existing = await tagActor.GetByIdAsync(id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existing.error.Message) || existing.data.SiteId != siteContext.SiteId)
            return TypedResults.NotFound(existing.error);

        var result = await tagActor.UpdateAsync(actorRequest, cancellationToken);
        if (!string.IsNullOrWhiteSpace(result.error.Message))
            return TypedResults.BadRequest(result.error);

        var count = await CountTagContentAsync(query, siteContext.SiteId, id, cancellationToken);
        return TypedResults.Ok(ToDetail(result.data, count));
    }

    /// <summary>
    /// Rejects current-site tags referenced by posts, then delegates deletion by identifier.
    /// </summary>
    /// <remarks>
    /// The preflight count is site-scoped, but this handler does not independently load the tag to
    /// verify ownership before the actor's identifier-only delete.
    /// </remarks>
    private static async Task<IResult> DeleteTag(
        long id,
        [FromServices] IAeroTagActor tagActor,
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(TagsApi));
        logger.LogDebug("Deleting tag {Id}", id);
        var count = await CountTagContentAsync(query, siteContext.SiteId, id, cancellationToken);
        if (count > 0)
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Tag is in use",
                Detail = "Remove the tag from posts before deleting it.",
                Status = StatusCodes.Status400BadRequest
            });

        var result = await tagActor.DeleteAsync(new ActorDeleteTagRequest(id), cancellationToken);
        return string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.Ok(true)
            : TypedResults.BadRequest(result.error);
    }

    /// <summary>
    /// Counts in-memory tag membership after loading all posts for one site.
    /// </summary>
    private static async Task<Dictionary<long, int>> GetContentCountsAsync(
        IQuerySession query,
        IEnumerable<long> tagIds,
        long siteId,
        CancellationToken cancellationToken)
    {
        var ids = tagIds.Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        var posts = await query.Query<PostDocument>()
            .Where(x => x.SiteId == siteId)
            .ToListAsync(cancellationToken);

        return ids.ToDictionary(id => id, id => posts.Count(post => post.TagIds.Contains(id)));
    }

    /// <summary>
    /// Adapts the batched tag counter to one identifier.
    /// </summary>
    private static Task<int> CountTagContentAsync(
        IQuerySession query,
        long siteId,
        long tagId,
        CancellationToken cancellationToken)
        => GetContentCountsAsync(query, [tagId], siteId, cancellationToken)
            .ContinueWith(task => task.Result.GetValueOrDefault(tagId), cancellationToken);

    /// <summary>
    /// Projects an actor model into the tag list contract.
    /// </summary>
    private static TagSummary ToSummary(TagViewModel vm, int count)
        => new(vm.Id, vm.Name ?? string.Empty, vm.Slug ?? string.Empty, count);

    /// <summary>
    /// Projects an actor model into the tag detail contract.
    /// </summary>
    private static TagDetail ToDetail(TagViewModel vm, int count)
        => new(vm.Id, vm.Name ?? string.Empty, vm.Slug ?? string.Empty, vm.Description, count, vm.CreatedOn.DateTime);
}
