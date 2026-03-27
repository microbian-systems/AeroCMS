using Aero.Cms.Core;
using Aero.Cms.Core.Http.Clients;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Headless.Api.v1;

/// <summary>
/// Admin API for media asset management.
/// </summary>
public static class MediaApi
{
    /// <summary>
    /// Maps the Media Admin API endpoints.
    /// </summary>
    public static void MapMediaApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/media")
            .WithTags("Admin - Media");

        group.MapGet("/", GetAllMedia)
            .WithName("GetAllMedia");

        group.MapGet("/details/{id:long}", GetMediaById)
            .WithName("GetMediaById");

        group.MapPost("/", CreateMedia)
            .WithName("UploadMedia");

        group.MapPut("/{id:long}", UpdateMedia)
            .WithName("UpdateMedia");

        group.MapDelete("/{id:long}", DeleteMedia)
            .WithName("DeleteMedia");
    }

    private static async Task<IResult> GetAllMedia(
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(MediaApi));
        try
        {
            var query = session.Query<MediaAsset>();

            IQueryable<MediaAsset> filteredQuery = query;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                filteredQuery = query.Where(x => x.FileName.ToLower().Contains(s) || (x.AltText != null && x.AltText.ToLower().Contains(s)));
            }

            var stats = new global::Marten.Linq.QueryStatistics();
            var media = await ((global::Marten.Linq.IMartenQueryable<MediaAsset>)filteredQuery)
                .OrderByDescending(x => x.CreatedOn)
                .Stats(out stats)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            var summaries = media.Select(m => new MediaSummary(
                m.Id,
                m.FileName,
                m.Url,
                m.MimeType,
                m.FileSize,
                m.CreatedOn.DateTime
            )).ToList();

            return TypedResults.Ok(new PagedResult<MediaSummary>(summaries, stats.TotalResults, skip, take));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving all media assets");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetMediaById(
        long id,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(MediaApi));
        try
        {
            var media = await session.LoadAsync<MediaAsset>(id, cancellationToken);

            if (media is null)
            {
                return TypedResults.NotFound(new { error = $"Media asset with ID {id} not found." });
            }

            var detail = new MediaDetail(
                media.Id,
                media.FileName,
                media.Url,
                media.MimeType,
                media.FileSize,
                media.CreatedOn.DateTime,
                media.Width,
                media.Height,
                media.AltText,
                media.Description
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving media asset for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> CreateMedia(
        [FromBody] UploadMediaRequest request,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(MediaApi));
        try
        {
            var media = new MediaAsset
            {
                Id = Snowflake.NewId(),
                FileName = request.FileName,
                MimeType = request.MimeType,
                FileSize = request.FileSize,
                AltText = request.AltText,
                Description = request.Description,
                Url = $"/media/{request.FileName}" // Simple placeholder URL
            };

            session.Store(media);
            await session.SaveChangesAsync(cancellationToken);

            var detail = new MediaDetail(
                media.Id,
                media.FileName,
                media.Url,
                media.MimeType,
                media.FileSize,
                media.CreatedOn.DateTime,
                media.Width,
                media.Height,
                media.AltText,
                media.Description
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating media asset");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateMedia(
        long id,
        [FromBody] UploadMediaRequest request, // Reusing request record for simplicity
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(MediaApi));
        try
        {
            var media = await session.LoadAsync<MediaAsset>(id, cancellationToken);

            if (media is null)
            {
                return TypedResults.NotFound(new { error = $"Media asset with ID {id} not found." });
            }

            media.FileName = request.FileName;
            media.MimeType = request.MimeType;
            media.FileSize = request.FileSize;
            media.AltText = request.AltText;
            media.Description = request.Description;

            session.Store(media);
            await session.SaveChangesAsync(cancellationToken);

            var detail = new MediaDetail(
                media.Id,
                media.FileName,
                media.Url,
                media.MimeType,
                media.FileSize,
                media.CreatedOn.DateTime,
                media.Width,
                media.Height,
                media.AltText,
                media.Description
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating media asset for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> DeleteMedia(
        long id,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(MediaApi));
        try
        {
            var media = await session.LoadAsync<MediaAsset>(id, cancellationToken);

            if (media is null)
            {
                return TypedResults.NotFound(new { error = $"Media asset with ID {id} not found." });
            }

            session.Delete(media);
            await session.SaveChangesAsync(cancellationToken);

            return TypedResults.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting media asset for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }
}
