using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Serialization;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Headless.Areas.Api.v1;

/// <summary>
/// Admin API for content item management.
/// </summary>
public static class ContentItemsApi
{
    /// <summary>
    /// Maps the Content Items Admin API endpoints.
    /// </summary>
    public static void MapContentItemsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/content-items")
            .WithTags("Admin - Content Items")
            .RequireAuthorization();

        group.MapGet("/", ListContentItems)
            .WithName("ListContentItems");

        group.MapGet("/{alias}/{id:long}", GetContentItem)
            .WithName("GetContentItem");

        group.MapPost("/{alias}", CreateContentItem)
            .WithName("CreateContentItem");

        group.MapPut("/{alias}/{id:long}", UpdateContentItem)
            .WithName("UpdateContentItem");

        group.MapDelete("/{alias}/{id:long}", DeleteContentItem)
            .WithName("DeleteContentItem");

        group.MapPost("/{alias}/{id:long}/publish", PublishContentItem)
            .WithName("PublishContentItem");

        group.MapPost("/{alias}/{id:long}/unpublish", UnpublishContentItem)
            .WithName("UnpublishContentItem");
    }

    private static async Task<IResult> ListContentItems(
        [FromQuery] string contentType,
        [FromServices] IContentQueryService contentQueryService,
        [FromServices] ILoggerFactory loggerFactory,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            const long siteId = 1L;
            var result = await contentQueryService.GetByTypeAsync(siteId, contentType, skip, take, cancellationToken);
            if (result is Result<(IReadOnlyList<ContentItem> Items, long TotalCount), AeroError>.Ok ok)
            {
                var summaries = ok.Value.Items.Select(item =>
                {
                    var firstField = item.Fields.Values.FirstOrDefault();
                    var firstFieldValue = firstField.ValueKind != JsonValueKind.Undefined
                        ? firstField.GetRawText()
                        : null;

                    return new ContentItemSummary(
                        item.Id,
                        item.Title ?? string.Empty,
                        item.Slug,
                        item.ContentTypeAlias,
                        firstFieldValue,
                        item.PublicationState.ToString(),
                        item.PublishedOn,
                        item.VersionNumber
                    );
                }).ToList();

                return TypedResults.Ok(new PagedResult<ContentItemSummary>(summaries, ok.Value.TotalCount, skip, take));
            }

            if (result is Result<(IReadOnlyList<ContentItem> Items, long TotalCount), AeroError>.Failure failure)
            {
                logger.LogWarning("Failed to list content items for type '{ContentType}'. Error: {Error}", contentType, failure.Error);
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to list content items",
                    Detail = failure.Error.ToString(),
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return TypedResults.Problem("An unexpected error occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing content items for type={ContentType}", contentType);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetContentItem(
        string alias,
        long id,
        [FromServices] IContentService contentService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            var result = await contentService.LoadAsync(id, cancellationToken);
            if (result is Result<ContentItem, AeroError>.Ok { Value: not null } ok)
            {
                return TypedResults.Ok(MapToDetail(ok.Value));
            }

            if (result is Result<ContentItem, AeroError>.Failure failure)
            {
                logger.LogWarning("Content item '{Id}' not found. Error: {Error}", id, failure.Error);
                return TypedResults.NotFound();
            }

            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving content item for id={Id}", id);
            return TypedResults.NotFound();
        }
    }

    private static async Task<IResult> CreateContentItem(
        string alias,
        [FromBody] CreateContentItemRequest request,
        [FromServices] ContentCommandService commandService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            const long siteId = 1L;

            var fields = new Dictionary<string, JsonElement>();
            foreach (var (key, value) in request.Fields)
                fields[key] = JsonSerializer.SerializeToElement(value, BlockJsonContext.Default.Options);

            var item = new ContentItem
            {
                Id = Snowflake.NewId(),
                SiteId = siteId,
                ContentTypeAlias = alias,
                Title = request.Title,
                Slug = request.Slug,
                Fields = fields,
                SchedulePublishUtc = request.SchedulePublishUtc,
                ScheduleUnpublishUtc = request.ScheduleUnpublishUtc
            };

            var result = await commandService.SaveDraftAsync(item, cancellationToken);
            if (result is Result<ContentItem, AeroError>.Ok ok)
            {
                var detail = MapToDetail(ok.Value);
                return TypedResults.Created($"/{HttpConstants.ApiPrefix}admin/content-items/{alias}/{ok.Value.Id}", detail);
            }

            if (result is Result<ContentItem, AeroError>.Failure failure)
            {
                logger.LogWarning("Failed to create content item. Error: {Error}. Request: {@Request}", failure.Error, request);
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to create content item",
                    Detail = failure.Error.ToString(),
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return TypedResults.Problem("An unexpected error occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating content item. Request: {@Request}", request);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateContentItem(
        string alias,
        long id,
        [FromBody] CreateContentItemRequest request,
        [FromServices] IContentService contentService,
        [FromServices] ContentCommandService commandService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            var existingResult = await contentService.LoadAsync(id, cancellationToken);
            if (existingResult is Result<ContentItem, AeroError>.Failure)
            {
                logger.LogWarning("Content item '{Id}' not found for update", id);
                return TypedResults.NotFound();
            }

            var existing = ((Result<ContentItem, AeroError>.Ok)existingResult).Value;

            existing.Title = request.Title;
            existing.Slug = request.Slug;
            existing.SchedulePublishUtc = request.SchedulePublishUtc;
            existing.ScheduleUnpublishUtc = request.ScheduleUnpublishUtc;

            var fields = new Dictionary<string, JsonElement>();
            foreach (var (key, value) in request.Fields)
                fields[key] = JsonSerializer.SerializeToElement(value, BlockJsonContext.Default.Options);
            existing.Fields = fields;

            var result = await commandService.SaveDraftAsync(existing, cancellationToken);
            if (result is Result<ContentItem, AeroError>.Ok ok)
            {
                return TypedResults.Ok(MapToDetail(ok.Value));
            }

            if (result is Result<ContentItem, AeroError>.Failure failure)
            {
                logger.LogWarning("Failed to update content item '{Id}'. Error: {Error}. Request: {@Request}", id, failure.Error, request);
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to update content item",
                    Detail = failure.Error.ToString(),
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return TypedResults.Problem("An unexpected error occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating content item for id={Id}. Request: {@Request}", id, request);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> DeleteContentItem(
        string alias,
        long id,
        [FromServices] ContentCommandService commandService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            var result = await commandService.DeleteAsync(id, cancellationToken);
            if (result is Result<bool, AeroError>.Ok { Value: true })
            {
                return TypedResults.NoContent();
            }

            if (result is Result<bool, AeroError>.Failure failure)
            {
                logger.LogWarning("Failed to delete content item '{Id}'. Error: {Error}", id, failure.Error);
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to delete content item",
                    Detail = failure.Error.ToString(),
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting content item for id={Id}", id);
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to delete content item",
                Detail = ex.Message
            });
        }
    }

    private static async Task<IResult> PublishContentItem(
        string alias,
        long id,
        [FromServices] IContentService contentService,
        [FromServices] ContentCommandService commandService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            var loadResult = await contentService.LoadAsync(id, cancellationToken);
            if (loadResult is Result<ContentItem, AeroError>.Failure)
            {
                logger.LogWarning("Content item '{Id}' not found for publish", id);
                return TypedResults.NotFound(new { error = $"Content item with id '{id}' not found." });
            }

            var item = ((Result<ContentItem, AeroError>.Ok)loadResult).Value;
            var result = await commandService.PublishAsync(item, cancellationToken);

            if (result is Result<ContentItem, AeroError>.Ok ok)
            {
                logger.LogInformation("Published content item id={Id}, slug={Slug}", id, ok.Value.Slug);
                return TypedResults.Ok(MapToDetail(ok.Value));
            }

            if (result is Result<ContentItem, AeroError>.Failure failure)
            {
                logger.LogWarning("Failed to publish content item '{Id}'. Error: {Error}", id, failure.Error);
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to publish content item",
                    Detail = failure.Error.ToString(),
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return TypedResults.Problem("An unexpected error occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing content item id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UnpublishContentItem(
        string alias,
        long id,
        [FromServices] IContentService contentService,
        [FromServices] ContentCommandService commandService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            var loadResult = await contentService.LoadAsync(id, cancellationToken);
            if (loadResult is Result<ContentItem, AeroError>.Failure)
            {
                logger.LogWarning("Content item '{Id}' not found for unpublish", id);
                return TypedResults.NotFound(new { error = $"Content item with id '{id}' not found." });
            }

            var item = ((Result<ContentItem, AeroError>.Ok)loadResult).Value;
            item.PublicationState = ContentPublicationState.Draft;
            item.PublishedOn = null;

            var result = await commandService.SaveDraftAsync(item, cancellationToken);
            if (result is Result<ContentItem, AeroError>.Ok ok)
            {
                logger.LogInformation("Unpublished content item id={Id}, slug={Slug}", id, ok.Value.Slug);
                return TypedResults.Ok(MapToDetail(ok.Value));
            }

            if (result is Result<ContentItem, AeroError>.Failure failure)
            {
                logger.LogWarning("Failed to unpublish content item '{Id}'. Error: {Error}", id, failure.Error);
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to unpublish content item",
                    Detail = failure.Error.ToString(),
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return TypedResults.Problem("An unexpected error occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unpublishing content item id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static ContentItemDetail MapToDetail(ContentItem item)
    {
        return new ContentItemDetail(
            item.Id,
            item.Title ?? string.Empty,
            item.Slug,
            item.ContentTypeAlias,
            item.Fields,
            item.PublicationState.ToString(),
            item.PublishedOn,
            item.VersionNumber,
            item.SchedulePublishUtc,
            item.ScheduleUnpublishUtc
        );
    }
}
