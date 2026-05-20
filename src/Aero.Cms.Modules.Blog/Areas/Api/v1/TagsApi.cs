using Aero.Cms.Abstractions.Actors;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Blog.Areas.Api.v1;

/// <summary>
/// Thin admin API for tag management.
/// Handles input validation and delegates all logic to <see cref="IAeroTagActor"/> (Orleans grain).
/// </summary>
public static class TagsApi
{
    public static void MapTagsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/tags")
            .WithTags("Admin - Tags");

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

    private static async Task<IResult> GetAllTags(
        [FromServices] IAeroTagActor tagActor,
        CancellationToken cancellationToken = default)
    {
        var tags = await tagActor.GetAllAsync(cancellationToken);
        return TypedResults.Ok(tags);
    }

    private static async Task<IResult> GetTagById(
        long id,
        [FromServices] IAeroTagActor tagActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(TagsApi));
        logger.LogDebug("Getting tag {Id}", id);
        var result = await tagActor.GetByIdAsync(id, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> CreateTag(
        [FromBody] CreateTagRequest request,
        [FromServices] IValidator<CreateTagRequest> validator,
        [FromServices] IAeroTagActor tagActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(TagsApi));

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            logger.LogWarning("CreateTag validation failed: {Errors}",
                validationResult.Errors.Select(e => e.ErrorMessage));
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        logger.LogDebug("Creating tag {Name}", request.Name);
        var result = await tagActor.CreateAsync(request, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> UpdateTag(
        long id,
        [FromBody] UpdateTagRequest request,
        [FromServices] ILoggerFactory loggerFactory,
        IAeroTagActor tagActor,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(TagsApi));
        logger.LogDebug("Updating tag {Id}", id);
        var result = await tagActor.UpdateAsync(request with { Id = id }, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> DeleteTag(
        long id,
        [FromServices] IAeroTagActor tagActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(TagsApi));
        logger.LogDebug("Deleting tag {Id}", id);
        var result = await tagActor.DeleteAsync(new DeleteTagRequest(id), cancellationToken);
        return TypedResults.Ok(result);
    }
}
