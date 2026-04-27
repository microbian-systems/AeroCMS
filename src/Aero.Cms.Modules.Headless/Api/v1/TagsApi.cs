using Aero.Cms.Core;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Modules.Blog.Models;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Headless.Api.v1;

/// <summary>
/// Admin API for tag management.
/// </summary>
public static class TagsApi
{
    /// <summary>
    /// Maps the Tags Admin API endpoints.
    /// </summary>
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
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(TagsApi));
        try
        {
            var tags = await session.Query<Tag>()
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            var summaries = tags.Select(t => new TagSummary(
                t.Id,
                t.Name,
                t.Slug,
                0 // TODO: Get content count
            )).ToList();

            return TypedResults.Ok(summaries);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving all tags");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetTagById(
        long id,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(TagsApi));
        try
        {
            var tag = await session.LoadAsync<Tag>(id, cancellationToken);

            if (tag is null)
            {
                return TypedResults.NotFound(new { error = $"Tag with ID {id} not found." });
            }

            var detail = new TagDetail(
                tag.Id,
                tag.Name,
                tag.Slug,
                null, // Tag model in blog doesn't have description yet, but client expects it
                0,
                tag.CreatedOn.DateTime
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving tag for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> CreateTag(
        [FromBody] CreateTagRequest request,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(TagsApi));
        try
        {
            var tag = new Tag
            {
                Id = Snowflake.NewId(),
                Name = request.Name,
                Slug = request.Slug
            };

            session.Store(tag);
            await session.SaveChangesAsync(cancellationToken);

            var detail = new TagDetail(
                tag.Id,
                tag.Name,
                tag.Slug,
                request.Description,
                0,
                tag.CreatedOn.DateTime
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating tag");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateTag(
        long id,
        [FromBody] UpdateTagRequest request,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(TagsApi));
        try
        {
            var tag = await session.LoadAsync<Tag>(id, cancellationToken);

            if (tag is null)
            {
                return TypedResults.NotFound(new { error = $"Tag with ID {id} not found." });
            }

            tag.Name = request.Name;
            tag.Slug = request.Slug;

            session.Store(tag);
            await session.SaveChangesAsync(cancellationToken);

            var detail = new TagDetail(
                tag.Id,
                tag.Name,
                tag.Slug,
                request.Description,
                0,
                tag.CreatedOn.DateTime
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating tag for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> DeleteTag(
        long id,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(TagsApi));
        try
        {
            var tag = await session.LoadAsync<Tag>(id, cancellationToken);

            if (tag is null)
            {
                return TypedResults.NotFound(new { error = $"Tag with ID {id} not found." });
            }

            session.Delete(tag);
            await session.SaveChangesAsync(cancellationToken);

            return TypedResults.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting tag for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }
}
