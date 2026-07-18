using System.Globalization;
using System.Text.Json;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Http;

namespace Aero.Cms.Modules.Content.Areas.Api.v1;

/// <summary>
/// Thin admin API for content item management — delegates to
/// <see cref="IAeroContentItemActor"/> (Orleans grain).
/// </summary>
public static class ContentItemsApi
{
        /// <summary>
    /// MapContentItemsApi method.
    /// </summary>
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
        group.MapGet("/{alias}/{id:long}/translations", ListContentItemTranslations).WithName("ListContentItemTranslations");
        group.MapPost("/{alias}/{id:long}/translations", ForkContentItemToCulture).WithName("ForkContentItemToCulture");
    }

    private static async Task<IResult> ListContentItems(
        [FromQuery] string contentType,
        [FromServices] IAeroContentItemActor contentActor,
        [FromServices] IContentQueryService queryService,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            var siteId = siteContext.SiteId;
            if (siteId <= 0)
                return MissingSite();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchResult = await queryService.SearchAsync(
                    siteId,
                    contentType,
                    new Dictionary<string, string> { ["__search"] = search },
                    ct);

                if (searchResult is Result<IReadOnlyList<ContentItem>, AeroError>.Ok ok)
                {
                    var page = ok.Value.Skip(skip).Take(take).Select(MapToSummary).ToList();
                    return TypedResults.Ok(new PagedResult<ContentItemSummary>(page, ok.Value.Count, skip, take));
                }
            }

            var (items, totalCount) = await contentActor.GetByTypeAsync(siteId, contentType, skip, take, ct);
            var summaries = items.Select(MapToSummary).ToList();

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
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        var siteId = siteContext.SiteId;
        if (siteId <= 0)
            return MissingSite();

        var result = await contentActor.GetByIdAsync(id, ct);
        if (!IsCurrentSiteItem(result, siteId, alias))
            return TypedResults.NotFound();

        return TypedResults.Ok(MapToDetail(result.data));
    }

    private static async Task<IResult> CreateContentItem(
        string alias,
        [FromBody] CreateContentItemRequest request,
        [FromServices] IAeroContentItemActor contentActor,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            var siteId = siteContext.SiteId;
            if (siteId <= 0)
                return MissingSite();

            var fields = new Dictionary<string, JsonElement>();
            foreach (var (key, value) in request.Fields)
                fields[key] = value.Clone();

            var vm = new ContentItemViewModel
            {
                SiteId = siteId,
                ContentTypeAlias = alias,
                Title = request.Title,
                Slug = request.Slug,
                Culture = ResolveRequestCulture(request.Culture),
                FieldsJson = JsonSerializer.Serialize(fields, ContentJsonContext.Default.Options),
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
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            var siteId = siteContext.SiteId;
            if (siteId <= 0)
                return MissingSite();

            // Load existing to preserve fields not in request
            var existing = await contentActor.GetByIdAsync(id, ct);
            if (!IsCurrentSiteItem(existing, siteId, alias))
                return TypedResults.NotFound();

            var existingVm = existing.data;

            var fields = new Dictionary<string, JsonElement>();
            foreach (var (key, value) in request.Fields)
                fields[key] = value.Clone();

            existingVm.Title = request.Title;
            existingVm.Slug = request.Slug;
            if (!string.IsNullOrWhiteSpace(request.Culture))
            {
                existingVm.Culture = ResolveRequestCulture(request.Culture);
            }
            existingVm.FieldsJson = JsonSerializer.Serialize(fields, ContentJsonContext.Default.Options);
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

    private static string ResolveRequestCulture(string? culture) =>
        CultureInfo.GetCultureInfo(
            string.IsNullOrWhiteSpace(culture)
                ? CultureInfo.CurrentUICulture.Name
                : culture).Name;

    private static async Task<IResult> DeleteContentItem(
        string alias, long id,
        [FromServices] IAeroContentItemActor contentActor,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            var siteId = siteContext.SiteId;
            if (siteId <= 0)
                return MissingSite();

            var existing = await contentActor.GetByIdAsync(id, ct);
            if (!IsCurrentSiteItem(existing, siteId, alias))
                return TypedResults.NotFound();

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
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            var siteId = siteContext.SiteId;
            if (siteId <= 0)
                return MissingSite();

            var existing = await contentActor.GetByIdAsync(id, ct);
            if (!IsCurrentSiteItem(existing, siteId, alias))
                return TypedResults.NotFound();

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
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            var siteId = siteContext.SiteId;
            if (siteId <= 0)
                return MissingSite();

            var existing = await contentActor.GetByIdAsync(id, ct);
            if (!IsCurrentSiteItem(existing, siteId, alias))
                return TypedResults.NotFound();

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

    private static async Task<IResult> ListContentItemTranslations(
        string alias,
        long id,
        [FromServices] IAeroContentItemActor contentActor,
        [FromServices] IContentQueryService queryService,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            var siteId = siteContext.SiteId;
            if (siteId <= 0)
                return MissingSite();

            var source = await contentActor.GetByIdAsync(id, ct);
            if (!IsCurrentSiteItem(source, siteId, alias))
                return TypedResults.NotFound();

            var groupId = source.data.TranslationGroupId ?? source.data.Id;
            var variants = await queryService.ListCultureVariantsAsync(siteId, alias, groupId, ct);
            return variants is Result<IReadOnlyList<ContentItem>, AeroError>.Ok ok
                ? TypedResults.Ok(ok.Value.Select(MapToDetail).ToList())
                : TypedResults.Ok(new[] { MapToDetail(source.data) }.ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing content item translations for item {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> ForkContentItemToCulture(
        string alias,
        long id,
        [FromBody] ForkContentItemCultureRequest request,
        [FromServices] IAeroContentItemActor contentActor,
        [FromServices] IContentQueryService queryService,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentItemsApi));
        try
        {
            var siteId = siteContext.SiteId;
            if (siteId <= 0)
                return MissingSite();

            var source = await contentActor.GetByIdAsync(id, ct);
            if (!IsCurrentSiteItem(source, siteId, alias))
                return TypedResults.NotFound();

            var culture = NormalizeCulture(request.Culture);
            if (string.IsNullOrWhiteSpace(culture))
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Missing culture",
                    Detail = "Select a target culture for the translation.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var groupId = source.data.TranslationGroupId ?? source.data.Id;
            var variants = await queryService.ListCultureVariantsAsync(siteId, alias, groupId, ct);
            if (variants is Result<IReadOnlyList<ContentItem>, AeroError>.Ok variantsOk &&
                variantsOk.Value.Any(item => string.Equals(NormalizeCulture(item.Culture), culture, StringComparison.OrdinalIgnoreCase)))
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Translation already exists",
                    Detail = $"A '{culture}' translation already exists for this entry.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var fork = new ContentItemViewModel
            {
                Id = Snowflake.NewId(),
                SiteId = siteId,
                ContentTypeAlias = alias,
                Title = source.data.Title,
                Slug = request.Slug.Trim().Trim('/'),
                FieldsJson = source.data.FieldsJson,
                TranslationGroupId = groupId,
                Culture = culture,
                SourceItemId = source.data.Id,
                PublicationState = ContentPublicationState.Draft
            };

            var result = await contentActor.SaveDraftAsync(fork, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to create content item translation",
                    Detail = result.error.Message,
                    Status = StatusCodes.Status400BadRequest
                })
                : TypedResults.Created($"/{HttpConstants.ApiPrefix}admin/content-items/{alias}/{result.data.Id}", MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating content item translation for item {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static ContentItemDetail MapToDetail(ContentItemViewModel vm)
    {
        var fields = string.IsNullOrWhiteSpace(vm.FieldsJson) || vm.FieldsJson == "{}"
            ? []
            : JsonSerializer.Deserialize(
                vm.FieldsJson,
                ContentJsonContext.Default.DictionaryStringJsonElement) ?? [];

        return new ContentItemDetail(
            vm.Id, vm.Title ?? string.Empty, vm.Slug, vm.ContentTypeAlias,
            fields, vm.PublicationState.ToString(), vm.PublishedOn,
            vm.VersionNumber, vm.SchedulePublishUtc, vm.ScheduleUnpublishUtc,
            vm.Culture, vm.TranslationGroupId, vm.SourceItemId);
    }

    private static ContentItemSummary MapToSummary(ContentItemViewModel item)
    {
        string? firstFieldValue = null;
        if (!string.IsNullOrEmpty(item.FieldsJson) && item.FieldsJson != "{}")
        {
            var fields = JsonSerializer.Deserialize(
                item.FieldsJson,
                ContentJsonContext.Default.DictionaryStringJsonElement);
            var firstField = fields?.Values.FirstOrDefault();
            if (firstField?.ValueKind != JsonValueKind.Undefined)
                firstFieldValue = firstField?.GetRawText();
        }

        return new ContentItemSummary(
            item.Id, item.Title ?? string.Empty, item.Slug,
            item.ContentTypeAlias, firstFieldValue,
            item.PublicationState.ToString(), item.PublishedOn, item.VersionNumber,
            item.Culture, item.TranslationGroupId, item.SourceItemId);
    }

    private static ContentItemSummary MapToSummary(ContentItem item)
    {
        var firstField = item.Fields.Values.FirstOrDefault();
        var firstFieldValue = firstField.ValueKind == JsonValueKind.Undefined
            ? null
            : firstField.GetRawText();

        return new ContentItemSummary(
            item.Id, item.Title ?? string.Empty, item.Slug,
            item.ContentTypeAlias, firstFieldValue,
            item.PublicationState.ToString(), item.PublishedOn, item.VersionNumber,
            item.Culture, item.TranslationGroupId, item.SourceItemId);
    }

    private static ContentItemDetail MapToDetail(ContentItem item)
        => new(
            item.Id,
            item.Title ?? string.Empty,
            item.Slug,
            item.ContentTypeAlias,
            item.Fields,
            item.PublicationState.ToString(),
            item.PublishedOn,
            item.VersionNumber,
            item.SchedulePublishUtc,
            item.ScheduleUnpublishUtc,
            item.Culture,
            item.TranslationGroupId,
            item.SourceItemId);

    private static string NormalizeCulture(string? culture)
        => culture?.Trim() ?? string.Empty;

    private static bool IsCurrentSiteItem(
        AeroRequestResponse<ContentItemViewModel> result,
        long siteId,
        string alias)
        => string.IsNullOrWhiteSpace(result.error.Message) &&
           result.data.SiteId == siteId &&
           string.Equals(result.data.ContentTypeAlias, alias, StringComparison.OrdinalIgnoreCase);

    private static IResult MissingSite()
        => TypedResults.BadRequest(new ProblemDetails
        {
            Title = "No current site selected",
            Detail = "Select a site in the manager before managing content entries.",
            Status = StatusCodes.Status400BadRequest
        });
}
