using System.Text.Json;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Blocks.Serialization;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Content.Areas.Api.v1;

/// <summary>
/// Thin admin API for content item management — delegates to
/// <see cref="IAeroContentItemActor"/> (Orleans grain).
/// </summary>
public static class ContentItemsApi
{
    public static void MapContentItemsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/content-items")
            .WithTags("Admin - Content Items")
            .RequireAuthorization();

        group.MapGet("/", ListContentItems).WithName("ListContentItems");
        group.MapGet("/{alias}/{id:long}", GetContentItem).WithName("GetContentItem");
        group.MapPost("/{alias}", CreateContentItem).WithName("CreateContentItem");
        group.MapPut("/{alias}/{id:long}", UpdateContentItem).WithName("UpdateContentItem");
        group.MapDelete("/{alias}/{id:long}", DeleteContentItem).WithName("DeleteContentItem");
        group.MapPost("/{alias}/{id:long}/publish", PublishContentItem).WithName("PublishContentItem");
        group.MapPost("/{alias}/{id:long}/unpublish", UnpublishContentItem).WithName("UnpublishContentItem");
    }

    private static async Task<IResult> ListContentItems(
        [FromQuery] string contentType,
        [FromServices] IAeroContentItemActor contentActor,
        [FromServices] ILoggerFactory loggerFactory,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            const long siteId = 1L;
            var (items, totalCount) = await contentActor.GetByTypeAsync(siteId, contentType, skip, take, ct);

            var summaries = items.Select(item =>
            {
                string? firstFieldValue = null;
                if (!string.IsNullOrEmpty(item.FieldsJson) && item.FieldsJson != "{}")
                {
                    var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(item.FieldsJson);
                    var firstField = fields?.Values.FirstOrDefault();
                    if (firstField?.ValueKind != JsonValueKind.Undefined)
                        firstFieldValue = firstField?.GetRawText();
                }

                return new ContentItemSummary(
                    item.Id, item.Title ?? string.Empty, item.Slug,
                    item.ContentTypeAlias, firstFieldValue,
                    item.PublicationState.ToString(), item.PublishedOn, item.VersionNumber);
            }).ToList();

            return TypedResults.Ok(new PagedResult<ContentItemSummary>(summaries, totalCount, skip, take));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing content items for type={ContentType}", contentType);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetContentItem(
        string alias, long id,
        [FromServices] IAeroContentItemActor contentActor,
        CancellationToken ct)
    {
        var result = await contentActor.GetByIdAsync(id, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound()
            : TypedResults.Ok(MapToDetail(result.data));
    }

    private static async Task<IResult> CreateContentItem(
        string alias,
        [FromBody] CreateContentItemRequest request,
        [FromServices] IAeroContentItemActor contentActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            const long siteId = 1L;

            var fields = new Dictionary<string, JsonElement>();
            foreach (var (key, value) in request.Fields)
                fields[key] = JsonSerializer.SerializeToElement(value, BlockJsonContext.Default.Options);

            var vm = new ContentItemViewModel
            {
                SiteId = siteId,
                ContentTypeAlias = alias,
                Title = request.Title,
                Slug = request.Slug,
                FieldsJson = JsonSerializer.Serialize(fields, BlockJsonContext.Default.Options),
                SchedulePublishUtc = request.SchedulePublishUtc,
                ScheduleUnpublishUtc = request.ScheduleUnpublishUtc
            };

            var result = await contentActor.SaveDraftAsync(vm, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? TypedResults.BadRequest(new ProblemDetails { Title = "Failed to create content item", Detail = result.error.Message, Status = StatusCodes.Status400BadRequest })
                : TypedResults.Created($"/{HttpConstants.ApiPrefix}admin/content-items/{alias}/{result.data.Id}", MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating content item");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateContentItem(
        string alias, long id,
        [FromBody] CreateContentItemRequest request,
        [FromServices] IAeroContentItemActor contentActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            // Load existing to preserve fields not in request
            var existing = await contentActor.GetByIdAsync(id, ct);
            if (!string.IsNullOrWhiteSpace(existing.error.Message))
                return TypedResults.NotFound();

            var existingVm = existing.data;

            var fields = new Dictionary<string, JsonElement>();
            foreach (var (key, value) in request.Fields)
                fields[key] = JsonSerializer.SerializeToElement(value, BlockJsonContext.Default.Options);

            existingVm.Title = request.Title;
            existingVm.Slug = request.Slug;
            existingVm.FieldsJson = JsonSerializer.Serialize(fields, BlockJsonContext.Default.Options);
            existingVm.SchedulePublishUtc = request.SchedulePublishUtc;
            existingVm.ScheduleUnpublishUtc = request.ScheduleUnpublishUtc;

            var result = await contentActor.SaveDraftAsync(existingVm, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? TypedResults.BadRequest(new ProblemDetails { Title = "Failed to update content item", Detail = result.error.Message, Status = StatusCodes.Status400BadRequest })
                : TypedResults.Ok(MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating content item {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> DeleteContentItem(
        string alias, long id,
        [FromServices] IAeroContentItemActor contentActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            var result = await contentActor.DeleteAsync(id, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? TypedResults.BadRequest(new ProblemDetails { Title = "Failed to delete content item", Detail = result.error.Message, Status = StatusCodes.Status400BadRequest })
                : TypedResults.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting content item {Id}", id);
            return TypedResults.BadRequest(new ProblemDetails { Title = "Failed to delete content item", Detail = ex.Message });
        }
    }

    private static async Task<IResult> PublishContentItem(
        string alias, long id,
        [FromServices] IAeroContentItemActor contentActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            var result = await contentActor.PublishAsync(id, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? TypedResults.NotFound(result.error)
                : TypedResults.Ok(MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing content item {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UnpublishContentItem(
        string alias, long id,
        [FromServices] IAeroContentItemActor contentActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            var result = await contentActor.UnpublishAsync(id, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? TypedResults.NotFound(result.error)
                : TypedResults.Ok(MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unpublishing content item {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static ContentItemDetail MapToDetail(ContentItemViewModel vm)
    {
        var fields = string.IsNullOrWhiteSpace(vm.FieldsJson) || vm.FieldsJson == "{}"
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(vm.FieldsJson, BlockJsonContext.Default.Options) ?? [];

        return new ContentItemDetail(
            vm.Id, vm.Title ?? string.Empty, vm.Slug, vm.ContentTypeAlias,
            fields, vm.PublicationState.ToString(), vm.PublishedOn,
            vm.VersionNumber, vm.SchedulePublishUtc, vm.ScheduleUnpublishUtc);
    }
}
