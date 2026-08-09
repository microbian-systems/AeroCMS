using System.Globalization;
using System.Text.Json;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Content.Search;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Http;

namespace Aero.Cms.Modules.Content.Areas.Api.v1;

/// <summary>
/// Thin admin API for content item management — delegates to
/// <see cref="IAeroContentItemActor"/> (Orleans grain).
/// </summary>
/// <remarks>
/// Every route requires authorization and derives its site boundary from <see cref="ISiteContext"/>.
/// Identifier-based operations load first and reject items whose site or type alias does not match
/// the current route.
/// </remarks>
public static class ContentItemsApi
{
    /// <summary>
    /// Maps authenticated content-item CRUD, publication, and translation endpoints.
    /// </summary>
    /// <param name="app">The endpoint route builder that receives the administrative routes.</param>
    public static void MapContentItemsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/content-items")
            .WithTags("Admin - Content Items")
            .RequireAuthorization();

        group.MapGet("/", ListContentItems)
            .RequireAuthorization("site:read")
            .WithName("ListContentItems");
        group.MapGet("/{alias}/{id:long}", GetContentItem)
            .RequireAuthorization("site:read")
            .WithName("GetContentItem");
        group.MapGet("/reference-options/{targetContentTypeId:long}", ListReferenceOptions)
            .RequireAuthorization("site:read")
            .WithName("ListContentReferenceOptions");
        group.MapGet("/reference-sources", ListCmsReferenceSources)
            .RequireAuthorization("site:read")
            .WithName("ListCmsContentReferenceSources");
        group.MapGet("/reference-sources/{source}/options", ListCmsReferenceOptions)
            .RequireAuthorization("site:read")
            .WithName("ListCmsContentReferenceOptions");
        group.MapPost("/{alias}", CreateContentItem)
            .RequireAuthorization("site:create")
            .WithName("CreateContentItem");
        group.MapPut("/{alias}/{id:long}", UpdateContentItem)
            .RequireAuthorization("site:update")
            .WithName("UpdateContentItem");
        group.MapDelete("/{alias}/{id:long}", DeleteContentItem)
            .RequireAuthorization("site:delete")
            .WithName("DeleteContentItem");
        group.MapPost("/{alias}/{id:long}/publish", PublishContentItem)
            .RequireAuthorization("site:update")
            .WithName("PublishContentItem");
        group.MapPost("/{alias}/{id:long}/unpublish", UnpublishContentItem)
            .RequireAuthorization("site:update")
            .WithName("UnpublishContentItem");
        group.MapGet("/{alias}/{id:long}/translations", ListContentItemTranslations)
            .RequireAuthorization("site:read")
            .WithName("ListContentItemTranslations");
        group.MapPost("/{alias}/{id:long}/translations", ForkContentItemToCulture)
            .RequireAuthorization("site:create")
            .WithName("ForkContentItemToCulture");
    }

    /// <summary>
    /// Lists a page of current-site items for a content type, optionally using field search.
    /// </summary>
    /// <returns>HTTP 200 with a page, HTTP 400 without a current site, or HTTP 500 on caught errors.</returns>
    /// <remarks>
    /// Search failures silently fall back to the actor's type listing. Skip and take are not
    /// validated here; search results are paged in memory.
    /// </remarks>
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

    /// <summary>
    /// Loads one content item and enforces the current site and route alias.
    /// </summary>
    /// <returns>HTTP 200, HTTP 400 without a site, or HTTP 404 for errors and boundary mismatches.</returns>
    /// <remarks>Actor and cancellation exceptions propagate because this handler has no catch block.</remarks>
    private static async Task<IResult> GetContentItem(
        string alias, long id,
        [FromServices] IAeroContentItemActor contentActor,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        var siteId = siteContext.SiteId;
        if (siteId <= 0)
            return MissingSite();

        var result = await contentActor.GetByIdAsync(id, siteId, ct);
        if (!IsCurrentSiteItem(result, siteId, alias, id))
            return TypedResults.NotFound();

        return TypedResults.Ok(MapToDetail(result.data));
    }

    /// <summary>
    /// Creates a draft content item for the current site and route alias.
    /// </summary>
    /// <returns>HTTP 201 on success, HTTP 400 for actor failures, or HTTP 500 on caught exceptions.</returns>
    /// <remarks>
    /// JSON fields are cloned before serialization. A blank culture uses the current UI culture;
    /// invalid culture names are caught and returned through a problem response.
    /// </remarks>
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
                ScheduleUnpublishUtc = request.ScheduleUnpublishUtc,
                ParentId = request.ParentId,
                SortOrder = request.SortOrder
            };

            var result = await contentActor.SaveDraftAsync(vm, siteId, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? ContentMutationFailure(logger, "Failed to create content item", result.error.Message, siteId, alias)
                : TypedResults.Created($"/{HttpConstants.ApiPrefix}admin/content-items/{alias}/{result.data.Id}", MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating content item");
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Replaces editable fields of an existing draft after site and alias validation.
    /// </summary>
    /// <returns>HTTP 200, HTTP 404 for a boundary mismatch, HTTP 400 for actor failure, or HTTP 500.</returns>
    /// <remarks>
    /// The submitted field dictionary replaces the stored field bag. Culture is retained only when
    /// the request culture is blank; all other editable values are overwritten.
    /// </remarks>
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
            var existing = await contentActor.GetByIdAsync(id, siteId, ct);
            if (!IsCurrentSiteItem(existing, siteId, alias, id))
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
            existingVm.ParentId = request.ParentId;
            existingVm.SortOrder = request.SortOrder;

            var result = await contentActor.SaveDraftAsync(existingVm, siteId, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? ContentMutationFailure(logger, "Failed to update content item", result.error.Message, siteId, alias, id)
                : TypedResults.Ok(MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating content item {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Returns a canonical culture name, using the current UI culture for blank input.
    /// </summary>
    /// <exception cref="CultureNotFoundException">Thrown when a nonblank culture is invalid.</exception>
    private static string ResolveRequestCulture(string? culture) =>
        CultureInfo.GetCultureInfo(
            string.IsNullOrWhiteSpace(culture)
                ? CultureInfo.CurrentUICulture.Name
                : culture).Name;

    /// <summary>
    /// Deletes an item only after validating its current-site and alias ownership.
    /// </summary>
    /// <returns>HTTP 204, HTTP 404 for a boundary mismatch, HTTP 400 for deletion errors, or HTTP 400 on exceptions.</returns>
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

            var existing = await contentActor.GetByIdAsync(id, siteId, ct);
            if (!IsCurrentSiteItem(existing, siteId, alias, id))
                return TypedResults.NotFound();

            var result = await contentActor.DeleteAsync(id, siteId, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? ContentMutationFailure(logger, "Failed to delete content item", result.error.Message, siteId, alias, id)
                : TypedResults.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting content item {Id}", id);
            return TypedResults.BadRequest(new ProblemDetails { Title = "Failed to delete content item", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Publishes an item after validating its current-site and alias ownership.
    /// </summary>
    /// <returns>HTTP 200, HTTP 404 for lookup failures, HTTP 400 for mutation failures, or HTTP 500 on exceptions.</returns>
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

            var existing = await contentActor.GetByIdAsync(id, siteId, ct);
            if (!IsCurrentSiteItem(existing, siteId, alias, id))
                return TypedResults.NotFound();

            var result = await contentActor.PublishAsync(id, siteId, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? ContentMutationFailure(logger, "Failed to publish content item", result.error.Message, siteId, alias, id)
                : TypedResults.Ok(MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing content item {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Returns a published item to draft after validating its current-site and alias ownership.
    /// </summary>
    /// <returns>HTTP 200, HTTP 404 for lookup failures, HTTP 400 for mutation failures, or HTTP 500 on exceptions.</returns>
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

            var existing = await contentActor.GetByIdAsync(id, siteId, ct);
            if (!IsCurrentSiteItem(existing, siteId, alias, id))
                return TypedResults.NotFound();

            var result = await contentActor.UnpublishAsync(id, siteId, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? ContentMutationFailure(logger, "Failed to unpublish content item", result.error.Message, siteId, alias, id)
                : TypedResults.Ok(MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unpublishing content item {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Lists culture variants in the source item's translation group.
    /// </summary>
    /// <returns>
    /// HTTP 200 with query results, or with only the source when the variant query fails; HTTP 404
    /// for a boundary mismatch; or HTTP 500 on caught exceptions.
    /// </returns>
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

            var source = await contentActor.GetByIdAsync(id, siteId, ct);
            if (!IsCurrentSiteItem(source, siteId, alias, id))
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

    /// <summary>
    /// Creates a draft translation by copying the source title and serialized fields.
    /// </summary>
    /// <returns>HTTP 201 on success, HTTP 400 for invalid or duplicate targets, HTTP 404, or HTTP 500.</returns>
    /// <remarks>
    /// Culture input is trimmed but not canonicalized. If variant discovery fails, duplicate-culture
    /// detection is skipped. The fork receives a new Snowflake identifier and does not copy scheduling
    /// or publication timestamps.
    /// </remarks>
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

            var source = await contentActor.GetByIdAsync(id, siteId, ct);
            if (!IsCurrentSiteItem(source, siteId, alias, id))
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
                Id = 0,
                SiteId = siteId,
                ContentTypeAlias = alias,
                Title = source.data.Title,
                Slug = request.Slug.Trim().Trim('/'),
                FieldsJson = source.data.FieldsJson,
                TranslationGroupId = groupId,
                Culture = culture,
                SourceItemId = source.data.Id,
                ParentId = null,
                SortOrder = source.data.SortOrder,
                PublicationState = ContentPublicationState.Draft
            };

            var result = await contentActor.SaveDraftAsync(fork, siteId, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? ContentMutationFailure(logger, "Failed to create content item translation", result.error.Message, siteId, alias, id)
                : TypedResults.Created($"/{HttpConstants.ApiPrefix}admin/content-items/{alias}/{result.data.Id}", MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating content item translation for item {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Deserializes a view model's field JSON and projects the detailed HTTP contract.
    /// </summary>
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
            vm.Culture, vm.TranslationGroupId, vm.SourceItemId,
            vm.ParentId, vm.SortOrder);
    }

    /// <summary>
    /// Projects a view model summary and exposes the first field as raw JSON text.
    /// </summary>
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
            item.Culture, item.TranslationGroupId, item.SourceItemId,
            item.ParentId, item.SortOrder);
    }

    /// <summary>
    /// Projects a persisted item summary and exposes the first field as raw JSON text.
    /// </summary>
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
            item.Culture, item.TranslationGroupId, item.SourceItemId,
            item.ParentId, item.SortOrder);
    }

    /// <summary>
    /// Projects a persisted content item into the detailed HTTP contract.
    /// </summary>
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
            item.SourceItemId,
            item.ParentId,
            item.SortOrder);

    /// <summary>
    /// Trims culture input without validating or canonicalizing it.
    /// </summary>
    private static string NormalizeCulture(string? culture)
        => culture?.Trim() ?? string.Empty;

    /// <summary>
    /// Accepts an actor response only when it succeeded and matches identifier, site, and type alias.
    /// </summary>
    private static bool IsCurrentSiteItem(
        AeroRequestResponse<ContentItemViewModel> result,
        long siteId,
        string alias,
        long id)
        => string.IsNullOrWhiteSpace(result.error.Message) &&
           result.data.Id == id &&
           result.data.SiteId == siteId &&
           string.Equals(result.data.ContentTypeAlias, alias, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Creates the standard HTTP 400 response for an absent current-site selection.
    /// </summary>
    private static IResult MissingSite()
        => TypedResults.BadRequest(new ProblemDetails
        {
            Title = "No current site selected",
            Detail = "Select a site in the manager before managing content entries.",
            Status = StatusCodes.Status400BadRequest
        });

    private static IResult ContentMutationFailure(
        ILogger logger,
        string title,
        string reason,
        long siteId,
        string alias,
        long? itemId = null)
    {
        logger.LogWarning(
            "Content mutation rejected for site {SiteId}, type {ContentType}, item {ContentItemId}: {Reason}",
            siteId,
            alias,
            itemId,
            reason);

        return TypedResults.BadRequest(new ProblemDetails
        {
            Title = title,
            Detail = "The requested content mutation could not be completed.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    private static async Task<IResult> ListCmsReferenceSources(
        [FromServices] IEnumerable<IContentReferenceSourceProvider> providers,
        [FromServices] IContentTypeService contentTypeService,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        if (siteContext.SiteId <= 0)
        {
            return MissingSite();
        }

        var sources = providers
            .Select(provider => new CmsContentReferenceSource(
                provider.SourceKey,
                provider.DisplayName))
            .ToList();
        var types = await contentTypeService.GetAllAsync(
            siteContext.SiteId,
            ct);
        if (types is Result<IReadOnlyList<ContentTypeDefinition>, AeroError>.Ok ok)
        {
            sources.AddRange(
                ok.Value
                    .Where(type => type.AllowPublicUrl)
                    .Select(type => new CmsContentReferenceSource(
                        CmsContentReferenceSources.ForContentType(type.Alias),
                        $"{type.Name} entries")));
        }

        var ordered = sources
            .DistinctBy(source => source.Key, StringComparer.OrdinalIgnoreCase)
            .OrderBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return TypedResults.Ok<IReadOnlyList<CmsContentReferenceSource>>(ordered);
    }

    private static async Task<IResult> ListCmsReferenceOptions(
        string source,
        [FromServices] IEnumerable<IContentReferenceSourceProvider> providers,
        [FromServices] IContentQueryService queryService,
        [FromServices] IContentTypeService contentTypeService,
        [FromServices] ISiteContext siteContext,
        [FromQuery] string? culture = null,
        [FromQuery] string? search = null,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        if (siteContext.SiteId <= 0)
        {
            return MissingSite();
        }

        if (CmsContentReferenceSources.TryGetContentTypeAlias(
                source,
                out var contentTypeAlias))
        {
            var type = await contentTypeService.GetByAliasAsync(
                siteContext.SiteId,
                contentTypeAlias,
                ct);
            if (type is not Result<ContentTypeDefinition, AeroError>.Ok
                {
                    Value: { AllowPublicUrl: true }
                })
            {
                return TypedResults.NotFound();
            }

            var boundedTake = Math.Clamp(take, 1, 100);
            var searchResult = await queryService.SearchIndexAsync(
                new ContentSearchRequest(
                    siteContext.SiteId,
                    contentTypeAlias,
                    search?.Trim() ?? string.Empty,
                    string.IsNullOrWhiteSpace(culture)
                        ? null
                        : culture.Trim(),
                    ContentSearchMode.FullText,
                    PublishedOnly: false,
                    Skip: 0,
                    Take: boundedTake,
                    ExactFilters: new Dictionary<string, string>(
                        StringComparer.Ordinal)),
                ct);
            return searchResult switch
            {
                Result<ContentSearchResult>.Ok ok =>
                    TypedResults.Ok<IReadOnlyList<CmsContentReferenceOption>>(
                        ok.Value.Items
                            .Select(item => new CmsContentReferenceOption(
                                item.Id.ToString(CultureInfo.InvariantCulture),
                                item.Title ?? item.Slug,
                                item.Slug,
                                item.Culture))
                            .ToArray()),
                Result<ContentSearchResult>.Failure failure =>
                    TypedResults.Problem(failure.Error.ToString()),
                _ => TypedResults.Problem(
                    "The content-entry page options could not be loaded.")
            };
        }

        var provider = providers.FirstOrDefault(candidate => string.Equals(
            candidate.SourceKey,
            source,
            StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            return TypedResults.NotFound();
        }

        var result = await provider.SearchAsync(
            siteContext.SiteId,
            culture,
            search,
            Math.Clamp(take, 1, 100),
            ct);
        return result switch
        {
            Result<IReadOnlyList<CmsContentReferenceOption>>.Ok ok =>
                TypedResults.Ok(ok.Value),
            Result<IReadOnlyList<CmsContentReferenceOption>>.Failure failure =>
                TypedResults.Problem(failure.Error.ToString()),
            _ => TypedResults.Problem("The reference options could not be loaded.")
        };
    }

    /// <summary>
    /// Returns bounded, site-scoped options for flat reference pickers.
    /// </summary>
    private static async Task<IResult> ListReferenceOptions(
        long targetContentTypeId,
        [FromServices] IContentQueryService queryService,
        [FromServices] IContentTypeService contentTypeService,
        [FromServices] ISiteContext siteContext,
        [FromQuery] string? culture = null,
        [FromQuery] string? search = null,
        [FromQuery] string? filterField = null,
        [FromQuery] string? filterValue = null,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var siteId = siteContext.SiteId;
        if (siteId <= 0)
        {
            return MissingSite();
        }

        var contentType = await contentTypeService.GetByIdAsync(
            siteId,
            targetContentTypeId,
            ct);
        if (contentType is not Result<ContentTypeDefinition, AeroError>.Ok typeOk)
        {
            return TypedResults.NotFound();
        }

        var filters = new Dictionary<string, string>(
            StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(culture))
        {
            filters["__culture"] = culture;
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            filters["__search"] = search;
        }

        if (!string.IsNullOrWhiteSpace(filterField)
            || !string.IsNullOrWhiteSpace(filterValue))
        {
            if (string.IsNullOrWhiteSpace(filterField)
                || string.IsNullOrWhiteSpace(filterValue)
                || !typeOk.Value.Fields.Any(field =>
                    string.Equals(
                        field.Name,
                        filterField,
                        StringComparison.Ordinal)
                    && field.FieldType == ContentFieldTypes.Reference))
            {
                return TypedResults.BadRequest(
                    new ProblemDetails
                    {
                        Title = "Invalid reference filter",
                        Detail = "The reference filter must name a reference field on the target content type."
                    });
            }

            filters[filterField] = filterValue;
        }

        var result = await queryService.SearchAsync(
            siteId,
            typeOk.Value.Alias,
            filters,
            ct);
        return result switch
        {
            Result<IReadOnlyList<ContentItem>, AeroError>.Ok ok =>
                TypedResults.Ok<IReadOnlyList<ContentReferenceOption>>(
                    ok.Value
                        .OrderBy(item => item.Title)
                        .ThenBy(item => item.Id)
                        .Take(Math.Clamp(take, 1, 100))
                        .Select(item => new ContentReferenceOption(
                            item.Id,
                            item.Title ?? item.Slug,
                            item.Slug,
                            item.Culture))
                        .ToList()),
            Result<IReadOnlyList<ContentItem>, AeroError>.Failure failure =>
                TypedResults.Problem(failure.Error.ToString()),
            _ => TypedResults.Problem("Unexpected content reference query result.")
        };
    }
}
