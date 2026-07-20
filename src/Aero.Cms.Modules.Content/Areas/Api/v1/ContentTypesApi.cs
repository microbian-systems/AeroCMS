using System.Text.Json;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Core.Http;

namespace Aero.Cms.Modules.Content.Areas.Api.v1;

/// <summary>
/// Thin admin API for content type definition management — delegates to
/// <see cref="IAeroContentTypeActor"/> (Orleans grain).
/// </summary>
/// <remarks>Every route requires authorization and scopes actor operations to the current site.</remarks>
public static class ContentTypesApi
{
        /// <summary>
    /// Maps authenticated content-type listing and mutation endpoints.
    /// </summary>
    /// <param name="app">The endpoint route builder that receives the administrative routes.</param>
public static void MapContentTypesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/content-types")
            .WithTags("Admin - Content Types")
            .RequireAuthorization();

        group.MapGet("/", ListContentTypes).RequireAuthorization("site:read").WithName("ListContentTypes");
        group.MapGet("/{alias}", GetContentTypeByAlias).RequireAuthorization("site:read").WithName("GetContentTypeByAlias");
        group.MapPost("/", CreateContentType).RequireAuthorization("site:create").WithName("CreateContentType");
        group.MapPut("/{alias}", UpdateContentType).RequireAuthorization("site:update").WithName("UpdateContentType");
        group.MapDelete("/{alias}", DeleteContentType).RequireAuthorization("site:delete").WithName("DeleteContentType");
    }

    /// <summary>
    /// Lists current-site content types and counts their items sequentially.
    /// </summary>
    /// <returns>HTTP 200, HTTP 400 without a current site, or HTTP 500 on caught exceptions.</returns>
    /// <remarks>A failed per-type count is represented as zero rather than failing the response.</remarks>
    private static async Task<IResult> ListContentTypes(
        [FromServices] IAeroContentTypeActor contentTypeActor,
        [FromServices] IContentQueryService contentQueryService,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentTypesApi));
        try
        {
            var siteId = siteContext.SiteId;
            if (siteId <= 0)
                return MissingSite();

            var types = await contentTypeActor.GetAllAsync(siteId, ct);
            var summaries = new List<ContentTypeSummary>(types.Count);
            foreach (var t in types)
            {
                var fields = string.IsNullOrWhiteSpace(t.FieldsJson) || t.FieldsJson == "[]"
                    ? []
                    : JsonSerializer.Deserialize(
                        t.FieldsJson,
                        ContentJsonContext.Default.ListContentFieldDefinition) ?? [];

                var itemCount = 0L;
                var countResult = await contentQueryService.CountByTypeAsync(siteId, t.Alias, ct);
                if (countResult is Result<long, AeroError>.Ok ok)
                {
                    itemCount = ok.Value;
                }

                summaries.Add(new ContentTypeSummary(
                    t.Alias, t.Name, t.Description, t.Category,
                    t.AllowPublicUrl, t.HideFromSearch, fields.Count,
                    !string.IsNullOrWhiteSpace(t.ScribanTemplate), itemCount));
            }

            return TypedResults.Ok(summaries);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing content types");
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Loads one current-site content type by alias.
    /// </summary>
    /// <returns>HTTP 200 when found; HTTP 404 for absence and all caught exceptions.</returns>
    private static async Task<IResult> GetContentTypeByAlias(
        string alias,
        [FromServices] IAeroContentTypeActor contentTypeActor,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentTypesApi));
        try
        {
            var siteId = siteContext.SiteId;
            if (siteId <= 0)
                return MissingSite();

            var type = await contentTypeActor.GetByAliasAsync(siteId, alias, ct);
            return type is not null
                ? TypedResults.Ok(MapToDetail(type))
                : TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving content type {Alias}", alias);
            return TypedResults.NotFound();
        }
    }

    /// <summary>
    /// Creates a content-type definition under the current site.
    /// </summary>
    /// <returns>HTTP 201, HTTP 400 for actor failure, or HTTP 500 on caught exceptions.</returns>
    private static async Task<IResult> CreateContentType(
        [FromBody] CreateContentTypeRequest request,
        [FromServices] IAeroContentTypeActor contentTypeActor,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentTypesApi));
        try
        {
            var siteId = siteContext.SiteId;
            if (siteId <= 0)
                return MissingSite();

            var vm = new ContentTypeViewModel
            {
                SiteId = siteId,
                Alias = request.Alias,
                Name = request.Name,
                Description = request.Description,
                Category = request.Category,
                Icon = request.Icon,
                AllowPublicUrl = request.AllowPublicUrl,
                HideFromSearch = request.HideFromSearch,
                FieldsJson = JsonSerializer.Serialize(
                    request.Fields.ToList(),
                    ContentJsonContext.Default.ListContentFieldDefinition),
                ScribanTemplate = request.ScribanTemplate,
                ScheduleConfig = request.ScheduleConfig
            };

            var result = await contentTypeActor.CreateAsync(vm, siteId, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? TypedResults.BadRequest(new ProblemDetails { Title = "Failed to create content type", Detail = result.error.Message, Status = StatusCodes.Status400BadRequest })
                : TypedResults.Created($"/{HttpConstants.ApiPrefix}admin/content-types/{result.data.Alias}", MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating content type");
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Replaces an existing current-site definition, including allowing its alias to change.
    /// </summary>
    /// <returns>HTTP 200, HTTP 404 when the route alias is absent, HTTP 400 for actor failure, or HTTP 500.</returns>
    /// <remarks>Fields and all mutable definition properties are replaced from the request.</remarks>
    private static async Task<IResult> UpdateContentType(
        string alias,
        [FromBody] CreateContentTypeRequest request,
        [FromServices] IAeroContentTypeActor contentTypeActor,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentTypesApi));
        try
        {
            var siteId = siteContext.SiteId;
            if (siteId <= 0)
                return MissingSite();

            var existing = await contentTypeActor.GetByAliasAsync(siteId, alias, ct);
            if (existing is null)
                return TypedResults.NotFound();

            existing.SiteId = siteId;
            existing.Alias = request.Alias;
            existing.Name = request.Name;
            existing.Description = request.Description;
            existing.Category = request.Category;
            existing.Icon = request.Icon;
            existing.AllowPublicUrl = request.AllowPublicUrl;
            existing.HideFromSearch = request.HideFromSearch;
            existing.FieldsJson = JsonSerializer.Serialize(
                request.Fields.ToList(),
                ContentJsonContext.Default.ListContentFieldDefinition);
            existing.ScribanTemplate = request.ScribanTemplate;
            existing.ScheduleConfig = request.ScheduleConfig;

            var result = await contentTypeActor.UpdateAsync(existing, siteId, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? TypedResults.BadRequest(new ProblemDetails { Title = "Failed to update content type", Detail = result.error.Message, Status = StatusCodes.Status400BadRequest })
                : TypedResults.Ok(MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating content type {Alias}", alias);
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Deletes a current-site content type by alias.
    /// </summary>
    /// <returns>HTTP 204 when deleted, HTTP 404 when absent, or HTTP 400 on caught exceptions.</returns>
    private static async Task<IResult> DeleteContentType(
        string alias,
        [FromServices] IAeroContentTypeActor contentTypeActor,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentTypesApi));
        try
        {
            var siteId = siteContext.SiteId;
            if (siteId <= 0)
                return MissingSite();

            var deleted = await contentTypeActor.DeleteAsync(siteId, alias, ct);
            return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting content type {Alias}", alias);
            return TypedResults.BadRequest(new ProblemDetails { Title = "Failed to delete content type", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Deserializes field definitions and projects a content-type detail response.
    /// </summary>
    private static ContentTypeDetail MapToDetail(ContentTypeViewModel vm)
    {
        var fields = string.IsNullOrWhiteSpace(vm.FieldsJson) || vm.FieldsJson == "[]"
            ? []
            : JsonSerializer.Deserialize(
                vm.FieldsJson,
                ContentJsonContext.Default.ListContentFieldDefinition) ?? [];

        return new ContentTypeDetail(
            vm.Alias, vm.Name, vm.Description, vm.Category,
            vm.Icon, vm.AllowPublicUrl, vm.HideFromSearch, fields, vm.ScribanTemplate,
            vm.ScheduleConfig);
    }

    /// <summary>
    /// Creates the standard HTTP 400 response for an absent current-site selection.
    /// </summary>
    private static IResult MissingSite()
        => TypedResults.BadRequest(new ProblemDetails
        {
            Title = "No current site selected",
            Detail = "Select a site in the manager before managing content types.",
            Status = StatusCodes.Status400BadRequest
        });
}
