using System.Text.Json;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Content;
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
public static class ContentTypesApi
{
        /// <summary>
    /// MapContentTypesApi method.
    /// </summary>
public static void MapContentTypesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/content-types")
            .WithTags("Admin - Content Types")
            .RequireAuthorization();

        group.MapGet("/", ListContentTypes).WithName("ListContentTypes");
        group.MapGet("/{alias}", GetContentTypeByAlias).WithName("GetContentTypeByAlias");
        group.MapPost("/", CreateContentType).WithName("CreateContentType");
        group.MapPut("/{alias}", UpdateContentType).WithName("UpdateContentType");
        group.MapDelete("/{alias}", DeleteContentType).WithName("DeleteContentType");
    }

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
                    : JsonSerializer.Deserialize<List<ContentFieldDefinition>>(t.FieldsJson) ?? [];

                var itemCount = 0L;
                var countResult = await contentQueryService.CountByTypeAsync(siteId, t.Alias, ct);
                if (countResult is Result<long, AeroError>.Ok ok)
                {
                    itemCount = ok.Value;
                }

                summaries.Add(new ContentTypeSummary(
                    t.Alias, t.Name, t.Description, t.Category,
                    t.AllowPublicUrl, t.HideFromSearch, fields.Count, t.RenderMode.ToString(),
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
                FieldsJson = JsonSerializer.Serialize(request.Fields.ToList()),
                ScribanTemplate = request.ScribanTemplate,
                RenderMode = Enum.Parse<ContentTypeRenderMode>(request.RenderMode),
                ScheduleConfig = request.ScheduleConfig
            };

            var result = await contentTypeActor.CreateAsync(vm, ct);
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
            existing.FieldsJson = JsonSerializer.Serialize(request.Fields.ToList());
            existing.ScribanTemplate = request.ScribanTemplate;
            existing.RenderMode = Enum.Parse<ContentTypeRenderMode>(request.RenderMode);
            existing.ScheduleConfig = request.ScheduleConfig;

            var result = await contentTypeActor.UpdateAsync(existing, ct);
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

    private static ContentTypeDetail MapToDetail(ContentTypeViewModel vm)
    {
        var fields = string.IsNullOrWhiteSpace(vm.FieldsJson) || vm.FieldsJson == "[]"
            ? []
            : JsonSerializer.Deserialize<List<ContentFieldDefinition>>(vm.FieldsJson) ?? [];

        return new ContentTypeDetail(
            vm.Alias, vm.Name, vm.Description, vm.Category,
            vm.Icon, vm.AllowPublicUrl, vm.HideFromSearch, fields, vm.ScribanTemplate,
            vm.RenderMode.ToString(), vm.ScheduleConfig);
    }

    private static IResult MissingSite()
        => TypedResults.BadRequest(new ProblemDetails
        {
            Title = "No current site selected",
            Detail = "Select a site in the manager before managing content types.",
            Status = StatusCodes.Status400BadRequest
        });
}
