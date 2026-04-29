using Aero.Cms.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Headless.Areas.Api.v1;

/// <summary>
/// Admin API for media asset management.
/// </summary>
public static class MediaApi
{
    private const long HtmlEditorImageMaxBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> HtmlEditorImageMimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
            ["image/gif"] = ".gif"
        };

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

        group.MapPost("/html-editor-image", UploadHtmlEditorImage)
            .DisableAntiforgery()
            .WithName("UploadHtmlEditorImage");

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

    private static async Task<IResult> UploadHtmlEditorImage(
        HttpRequest request,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] Microsoft.AspNetCore.Hosting.IWebHostEnvironment env,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(MediaApi));

        try
        {
            if (!request.HasFormContentType)
            {
                return TypedResults.BadRequest(new { error = "Expected multipart form upload." });
            }

            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();

            var validation = ValidateHtmlEditorImage(file);
            if (validation is not null)
            {
                return TypedResults.BadRequest(new { error = validation });
            }

            var safeExtension = GetSafeImageExtension(file!.ContentType, file.FileName);
            var safeBaseName = Path.GetFileNameWithoutExtension(file.FileName);
            safeBaseName = SanitizeFileName(string.IsNullOrWhiteSpace(safeBaseName) ? "html-editor-image" : safeBaseName);

            var storedFileName = $"{Snowflake.NewId()}-{safeBaseName}{safeExtension}";
            var directory = Path.Combine(GetWebRootPath(env), "media");
            Directory.CreateDirectory(directory);

            var filePath = Path.Combine(directory, storedFileName);
            await using (var stream = File.Create(filePath))
            await using (var upload = file.OpenReadStream())
            {
                await upload.CopyToAsync(stream, cancellationToken);
            }

            var url = $"/media/{storedFileName}";
            var media = new MediaAsset
            {
                Id = Snowflake.NewId(),
                FileName = storedFileName,
                MimeType = file.ContentType,
                FileSize = file.Length,
                Url = url,
                IsFolder = false
            };

            session.Store(media);
            await session.SaveChangesAsync(cancellationToken);

            return TypedResults.Ok(new { url, id = media.Id });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading HTML editor image");
            return TypedResults.Problem(detail: "Unable to upload image.");
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

    private static string? ValidateHtmlEditorImage(IFormFile? file)
    {
        if (file is null)
        {
            return "No file was uploaded.";
        }

        if (file.Length <= 0)
        {
            return "Uploaded file is empty.";
        }

        if (file.Length > HtmlEditorImageMaxBytes)
        {
            return "Uploaded image exceeds the 10 MB limit.";
        }

        if (!HtmlEditorImageMimeTypes.ContainsKey(file.ContentType))
        {
            return "Only JPEG, PNG, WebP, and GIF images are allowed.";
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return "Uploaded image must have a file extension.";
        }

        var expectedExtension = GetSafeImageExtension(file.ContentType, file.FileName);
        if (!string.Equals(extension, expectedExtension, StringComparison.OrdinalIgnoreCase)
            && !(string.Equals(file.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
                && string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)))
        {
            return "Uploaded image extension does not match the content type.";
        }

        return null;
    }

    private static string GetSafeImageExtension(string contentType, string fileName)
    {
        if (string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetExtension(fileName), ".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return ".jpeg";
        }

        return HtmlEditorImageMimeTypes[contentType];
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value
            .Where(ch => !invalid.Contains(ch))
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-')
            .ToArray();

        var sanitized = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "html-editor-image" : sanitized;
    }

    private static string GetWebRootPath(Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        => string.IsNullOrWhiteSpace(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath;
}
