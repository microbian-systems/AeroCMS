using Aero.Cms.Core;
using Aero.Cms.Abstractions.Http.Clients;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Aero.Core;
using System.IO;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/media")
            .WithTags("Admin - Media");

        group.MapPost("/folder", CreateFolder)
            .WithName("CreateFolder");

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
        [FromQuery] long? parentId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
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
                filteredQuery = filteredQuery.Where(x => x.FileName.ToLower().Contains(s) || (x.AltText != null && x.AltText.ToLower().Contains(s)));
            }
            else
            {
                filteredQuery = filteredQuery.Where(x => x.ParentId == parentId);
            }

            var stats = new global::Marten.Linq.QueryStatistics();
            var media = await ((global::Marten.Linq.IMartenQueryable<MediaAsset>)filteredQuery)
                .OrderByDescending(x => x.IsFolder)
                .ThenByDescending(x => x.CreatedOn)
                .Stats(out stats)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            var summaries = media.Select(m => new MediaSummary(
                m.Id,
                m.FileName,
                m.Url,
                m.MimeType ?? "application/octet-stream",
                m.FileSize,
                m.CreatedOn.DateTime,
                m.IsFolder,
                m.ParentId
            )).ToList();

            return TypedResults.Ok(new PagedResult<MediaSummary>(summaries, stats.TotalResults, skip, take));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving media assets (parentId={ParentId})", parentId);
            return TypedResults.Problem(detail: ex.Message);
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
                media.Description,
                media.IsFolder,
                media.ParentId
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving media asset for id={Id}", id);
            return TypedResults.Problem(detail: ex.Message);
        }
    }

    private static async Task<IResult> CreateFolder(
        [FromBody] CreateFolderRequest request,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(MediaApi));
        try
        {
            var folder = new MediaAsset
            {
                Id = Snowflake.NewId(),
                FileName = request.Name,
                IsFolder = true,
                ParentId = request.ParentId,
                MimeType = "folder",
                Url = "#"
            };

            session.Store(folder);
            await session.SaveChangesAsync(cancellationToken);

            var detail = new MediaDetail(folder.Id, folder.FileName, folder.Url, folder.MimeType, 0, folder.CreatedOn.DateTime, 0, 0, null, null, true, folder.ParentId);
            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating media folder");
            return TypedResults.Problem(detail: ex.Message);
        }
    }

    private static async Task<IResult> CreateMedia(
        [FromBody] UploadMediaRequest request,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] Microsoft.AspNetCore.Hosting.IWebHostEnvironment env,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(MediaApi));
        try
        {
            var url = $"/media/{request.FileName}";

            if (!string.IsNullOrEmpty(request.Base64Data))
            {
                var directory = Path.Combine(env.WebRootPath, "media");
                logger.LogInformation("Saving media file to: {Directory}. FileName: {FileName}", directory, request.FileName);

                if (!Directory.Exists(directory)) 
                {
                    logger.LogDebug("Directory does not exist. Creating: {Directory}", directory);
                    Directory.CreateDirectory(directory);
                }

                var filePath = Path.Combine(directory, request.FileName);
                logger.LogDebug("Converting Base64 data (Length: {Length}) to bytes...", request.Base64Data.Length);
                
                var data = Convert.FromBase64String(request.Base64Data);
                logger.LogInformation("Writing {Bytes} bytes to disk at {FilePath}...", data.Length, filePath);
                
                await File.WriteAllBytesAsync(filePath, data, cancellationToken);
                logger.LogDebug("File write complete.");
            }

            var media = new MediaAsset
            {
                Id = Snowflake.NewId(),
                FileName = request.FileName,
                MimeType = request.MimeType,
                FileSize = request.FileSize,
                AltText = request.AltText,
                Description = request.Description,
                Url = url,
                ParentId = request.ParentId,
                IsFolder = false
            };

            logger.LogDebug("Storing MediaAsset record in DB. ID: {Id}", media.Id);
            session.Store(media);
            await session.SaveChangesAsync(cancellationToken);
            logger.LogInformation("MediaAsset record persisted successfully for {Id}", media.Id);

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
                media.Description,
                false,
                media.ParentId
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating media asset");
            return TypedResults.Problem(detail: ex.Message);
        }
    }

    private static async Task<IResult> UpdateMedia(
        long id,
        [FromBody] UploadMediaRequest request,
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
            media.AltText = request.AltText;
            media.Description = request.Description;
            // Note: We don't update Url or Base64Data here for simplicity in this edit

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
                media.Description,
                media.IsFolder,
                media.ParentId
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating media asset for id={Id}", id);
            return TypedResults.Problem(detail: ex.Message);
        }
    }

    private static async Task<IResult> DeleteMedia(
        long id,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] Microsoft.AspNetCore.Hosting.IWebHostEnvironment env,
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

            if (!media.IsFolder)
            {
                var filePath = Path.Combine(env.WebRootPath, "media", media.FileName);
                if (File.Exists(filePath)) File.Delete(filePath);
            }
            else
            {
                // Optionally delete recursively if it was a real folder on disk, 
                // but here it's just a logical folder in DB.
            }

            session.Delete(media);
            await session.SaveChangesAsync(cancellationToken);

            return TypedResults.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting media asset for id={Id}", id);
            return TypedResults.Problem(detail: ex.Message);
        }
    }
}
