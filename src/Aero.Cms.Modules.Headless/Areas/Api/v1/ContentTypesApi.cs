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
using CreateContentTypeRequest = Aero.Cms.Abstractions.Http.Clients.CreateContentTypeRequest;

namespace Aero.Cms.Modules.Headless.Areas.Api.v1;

/// <summary>
/// Admin API for content type definition management.
/// </summary>
public static class ContentTypesApi
{
    /// <summary>
    /// Maps the Content Types Admin API endpoints.
    /// </summary>
    public static void MapContentTypesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/content-types")
            .WithTags("Admin - Content Types")
            .RequireAuthorization();

        group.MapGet("/", ListContentTypes)
            .WithName("ListContentTypes");

        group.MapGet("/{alias}", GetContentTypeByAlias)
            .WithName("GetContentTypeByAlias");

        group.MapPost("/", CreateContentType)
            .WithName("CreateContentType");

        group.MapPut("/{alias}", UpdateContentType)
            .WithName("UpdateContentType");

        group.MapDelete("/{alias}", DeleteContentType)
            .WithName("DeleteContentType");
    }

    private static async Task<IResult> ListContentTypes(
        [FromServices] IContentTypeService contentTypeService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentTypesApi));
        try
        {
            const long siteId = 1L;
            var result = await contentTypeService.GetAllAsync(siteId, cancellationToken);
            if (result is Result<IReadOnlyList<ContentTypeDefinition>, AeroError>.Ok ok)
            {
                var summaries = ok.Value.Select(t => new ContentTypeSummary(
                    t.Alias,
                    t.Name,
                    t.Description,
                    t.Category,
                    t.Fields.Count,
                    t.RenderMode.ToString(),
                    !string.IsNullOrWhiteSpace(t.ScribanTemplate),
                    0L
                )).ToList();

                return TypedResults.Ok(summaries);
            }

            if (result is Result<IReadOnlyList<ContentTypeDefinition>, AeroError>.Failure failure)
            {
                logger.LogWarning("Failed to list content types. Error: {Error}", failure.Error);
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to list content types",
                    Detail = failure.Error.ToString(),
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return TypedResults.Problem("An unexpected error occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing content types");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetContentTypeByAlias(
        string alias,
        [FromServices] IContentTypeService contentTypeService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentTypesApi));
        try
        {
            const long siteId = 1L;
            var result = await contentTypeService.GetByAliasAsync(siteId, alias, cancellationToken);
            if (result is Result<ContentTypeDefinition, AeroError>.Ok ok)
            {
                return TypedResults.Ok(MapToDetail(ok.Value));
            }

            if (result is Result<ContentTypeDefinition, AeroError>.Failure failure)
            {
                logger.LogWarning("Content type '{Alias}' not found. Error: {Error}", alias, failure.Error);
                return TypedResults.NotFound();
            }

            return TypedResults.Problem("An unexpected error occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving content type for alias={Alias}", alias);
            return TypedResults.NotFound();
        }
    }

    private static async Task<IResult> CreateContentType(
        [FromBody] CreateContentTypeRequest request,
        [FromServices] IContentTypeService contentTypeService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentTypesApi));
        try
        {
            var definition = new ContentTypeDefinition
            {
                Id = Snowflake.NewId(),
                Alias = request.Alias,
                Name = request.Name,
                Description = request.Description,
                Category = request.Category,
                Icon = request.Icon,
                Fields = request.Fields.ToList(),
                ScribanTemplate = request.ScribanTemplate,
                RenderMode = Enum.Parse<ContentTypeRenderMode>(request.RenderMode),
                ScheduleConfig = request.ScheduleConfig
            };

            var result = await contentTypeService.SaveAsync(definition, cancellationToken);
            if (result is Result<ContentTypeDefinition, AeroError>.Ok ok)
            {
                var detail = MapToDetail(ok.Value);
                return TypedResults.Created($"/{HttpConstants.ApiPrefix}admin/content-types/{ok.Value.Alias}", detail);
            }

            if (result is Result<ContentTypeDefinition, AeroError>.Failure failure)
            {
                logger.LogWarning("Failed to create content type. Error: {Error}. Request: {@Request}", failure.Error, request);
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to create content type",
                    Detail = failure.Error.ToString(),
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return TypedResults.Problem("An unexpected error occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating content type. Request: {@Request}", request);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateContentType(
        string alias,
        [FromBody] CreateContentTypeRequest request,
        [FromServices] IContentTypeService contentTypeService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentTypesApi));
        try
        {
            const long siteId = 1L;
            var existingResult = await contentTypeService.GetByAliasAsync(siteId, alias, cancellationToken);
            if (existingResult is Result<ContentTypeDefinition, AeroError>.Failure)
            {
                logger.LogWarning("Content type '{Alias}' not found for update", alias);
                return TypedResults.NotFound();
            }

            var existing = ((Result<ContentTypeDefinition, AeroError>.Ok)existingResult).Value;
            existing.Alias = request.Alias;
            existing.Name = request.Name;
            existing.Description = request.Description;
            existing.Category = request.Category;
            existing.Icon = request.Icon;
            existing.Fields = request.Fields.ToList();
            existing.ScribanTemplate = request.ScribanTemplate;
            existing.RenderMode = Enum.Parse<ContentTypeRenderMode>(request.RenderMode);
            existing.ScheduleConfig = request.ScheduleConfig;

            var result = await contentTypeService.SaveAsync(existing, cancellationToken);
            if (result is Result<ContentTypeDefinition, AeroError>.Ok ok)
            {
                return TypedResults.Ok(MapToDetail(ok.Value));
            }

            if (result is Result<ContentTypeDefinition, AeroError>.Failure failure)
            {
                logger.LogWarning("Failed to update content type '{Alias}'. Error: {Error}. Request: {@Request}", alias, failure.Error, request);
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to update content type",
                    Detail = failure.Error.ToString(),
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return TypedResults.Problem("An unexpected error occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating content type for alias={Alias}. Request: {@Request}", alias, request);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> DeleteContentType(
        string alias,
        [FromServices] IContentTypeService contentTypeService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ContentTypesApi));
        try
        {
            const long siteId = 1L;
            var existingResult = await contentTypeService.GetByAliasAsync(siteId, alias, cancellationToken);

            if (existingResult is Result<ContentTypeDefinition, AeroError>.Failure)
            {
                logger.LogWarning("Content type '{Alias}' not found for deletion", alias);
                return TypedResults.NotFound();
            }

            return TypedResults.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting content type '{Alias}'", alias);
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to delete content type",
                Detail = ex.Message
            });
        }
    }

    private static ContentTypeDetail MapToDetail(ContentTypeDefinition t)
    {
        return new ContentTypeDetail(
            t.Alias,
            t.Name,
            t.Description,
            t.Category,
            t.Icon,
            t.Fields,
            t.ScribanTemplate,
            t.RenderMode.ToString(),
            t.ScheduleConfig
        );
    }
}
