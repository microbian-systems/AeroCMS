using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Media.Areas.Api.v1;

/// <summary>
/// Thin admin API for media asset management.
/// File I/O remains here; persistence delegates to <see cref="IAeroMediaActor"/> (Orleans grain).
/// </summary>
public static class MediaApi
{
    private const long HtmlEditorImageMaxBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> HtmlEditorImageMimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg", ["image/png"] = ".png",
            ["image/webp"] = ".webp", ["image/gif"] = ".gif"
        };

        /// <summary>
    /// MapMediaApi method.
    /// </summary>
public static void MapMediaApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/media")
            .WithTags("Admin - Media");

        group.MapPost("/folder", CreateFolder).WithName("CreateFolder");
        group.MapGet("/", GetAllMedia).WithName("GetAllMedia");
        group.MapGet("/details/{id:long}", GetMediaById).WithName("GetMediaById");
        group.MapPost("/", CreateMedia).WithName("UploadMedia");
        group.MapPost("/html-editor-image", UploadHtmlEditorImage).DisableAntiforgery().WithName("UploadHtmlEditorImage");
        group.MapPut("/{id:long}", UpdateMedia).WithName("UpdateMedia");
        group.MapDelete("/{id:long}", DeleteMedia).WithName("DeleteMedia");
    }

    private static async Task<IResult> GetAllMedia(
        [FromQuery] long? parentId, [FromQuery] int skip, [FromQuery] int take, [FromQuery] string? search,
        [FromServices] IAeroMediaActor mediaActor, [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(MediaApi));
        logger.LogDebug("Listing media p={ParentId} s={Skip} t={Take}", parentId, skip, take);
        var (items, total) = await mediaActor.GetPagedAsync(parentId, skip, take, search, ct);
        var summaries = items.Select(m => new MediaSummary(m.Id, m.FileName ?? m.Title ?? "", m.Url ?? "",
            m.MimeType?.ToString() ?? "application/octet-stream", m.FileSizeInBytes, m.CreatedOn.DateTime,
            m.IsFolder, m.ParentId)).ToList();
        return TypedResults.Ok(new PagedResult<MediaSummary>(summaries, total, skip, take));
    }

    private static async Task<IResult> GetMediaById(
        long id, [FromServices] IAeroMediaActor mediaActor,
        [FromServices] ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(MediaApi));
        logger.LogDebug("Getting media {Id}", id);
        var result = await mediaActor.GetByIdAsync(id, ct);
        if (!string.IsNullOrEmpty(result.error?.Message))
            return TypedResults.NotFound(new { error = result.error.Message });
        var m = result.data;
        return TypedResults.Ok(new MediaDetail(m.Id, m.FileName ?? "", m.Url ?? "",
            m.MimeType?.ToString() ?? "", m.FileSizeInBytes, m.CreatedOn.DateTime,
            m.Dimensions.Width, m.Dimensions.Height, m.AltText, m.Description, m.IsFolder, m.ParentId));
    }

    private static async Task<IResult> CreateFolder(
        [FromBody] CreateFolderRequest request, [FromServices] IAeroMediaActor mediaActor,
        [FromServices] ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(MediaApi));
        logger.LogDebug("Creating folder {Name}", request.Name);
        var folder = new MediaAsset { FileName = request.Name, Url = $"/media/{request.Name}",
            IsFolder = true, ParentId = request.ParentId };
        var result = await mediaActor.SaveMediaAsync(new MediaViewModel
        {
            Title = request.Name, FileName = request.Name, Url = $"/media/{request.Name}",
            IsFolder = true, ParentId = request.ParentId
        }, ct);
        var m = result.data;
        return TypedResults.Ok(new MediaDetail(m.Id, m.FileName ?? "", m.Url ?? "", "", 0,
            m.CreatedOn.DateTime, 0, 0, null, null, true, m.ParentId));
    }

    private static async Task<IResult> CreateMedia(
        [FromBody] UploadMediaRequest request, [FromServices] IAeroMediaActor mediaActor,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] Microsoft.AspNetCore.Hosting.IWebHostEnvironment env, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(MediaApi));
        var url = $"/media/{request.FileName}";

        if (!string.IsNullOrEmpty(request.Base64Data))
        {
            var directory = Path.Combine(GetWebRootPath(env), "media");
            Directory.CreateDirectory(directory);
            var filePath = Path.Combine(directory, request.FileName);
            await File.WriteAllBytesAsync(filePath, Convert.FromBase64String(request.Base64Data), ct);
            logger.LogInformation("Saved {Bytes} bytes to {Path}", request.Base64Data.Length, filePath);
        }

        var mediaVm = new MediaViewModel
        {
            FileName = request.FileName, Title = request.FileName,
            MimeType = request.MimeType, FileSizeInBytes = request.FileSize,
            AltText = request.AltText, Description = request.Description,
            Url = url, ParentId = request.ParentId, IsFolder = false
        };
        var result = await mediaActor.SaveMediaAsync(mediaVm, ct);
        var m = result.data;
        return TypedResults.Ok(new MediaDetail(m.Id, m.FileName ?? "", m.Url ?? "", m.MimeType?.ToString() ?? "",
            m.FileSizeInBytes, m.CreatedOn.DateTime, m.Dimensions.Width, m.Dimensions.Height,
            m.AltText, m.Description, false, m.ParentId));
    }

    private static async Task<IResult> UploadHtmlEditorImage(
        HttpRequest request, [FromServices] IAeroMediaActor mediaActor,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] Microsoft.AspNetCore.Hosting.IWebHostEnvironment env, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(MediaApi));
        if (!request.HasFormContentType) return TypedResults.BadRequest(new { error = "Expected multipart form upload." });
        var form = await request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        var validation = ValidateHtmlEditorImage(file);
        if (validation is not null) return TypedResults.BadRequest(new { error = validation });

        var safeExtension = GetSafeImageExtension(file!.ContentType, file.FileName);
        var safeBaseName = SanitizeFileName(Path.GetFileNameWithoutExtension(file.FileName));
        var storedFileName = $"{Snowflake.NewId()}-{safeBaseName}{safeExtension}";

        var directory = Path.Combine(GetWebRootPath(env), "media");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, storedFileName);
        await using (var stream = File.Create(filePath))
        await using (var upload = file.OpenReadStream())
            await upload.CopyToAsync(stream, ct);

        var url = $"/media/{storedFileName}";
        var result = await mediaActor.SaveMediaAsync(new MediaViewModel
        {
            FileName = storedFileName, Title = storedFileName,
            MimeType = file.ContentType, FileSizeInBytes = file.Length,
            Url = url, IsFolder = false
        }, ct);
        return TypedResults.Ok(new { url, id = result.data.Id });
    }

    private static async Task<IResult> UpdateMedia(
        long id, [FromBody] UploadMediaRequest request,
        [FromServices] IAeroMediaActor mediaActor, [FromServices] ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(MediaApi));
        logger.LogDebug("Updating media {Id}", id);
        var result = await mediaActor.SaveMediaAsync(new MediaViewModel
        {
            Id = id, FileName = request.FileName, Title = request.FileName,
            AltText = request.AltText, Description = request.Description
        }, ct);
        if (!string.IsNullOrEmpty(result.error?.Message))
            return TypedResults.NotFound(new { error = result.error.Message });
        var m = result.data;
        return TypedResults.Ok(new MediaDetail(m.Id, m.FileName ?? "", m.Url ?? "", m.MimeType?.ToString() ?? "",
            m.FileSizeInBytes, m.CreatedOn.DateTime, m.Dimensions.Width, m.Dimensions.Height,
            m.AltText, m.Description, m.IsFolder, m.ParentId));
    }

    private static async Task<IResult> DeleteMedia(
        long id, [FromServices] IAeroMediaActor mediaActor,
        [FromServices] ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(MediaApi));
        logger.LogDebug("Deleting media {Id}", id);
        var result = await mediaActor.DeleteMediaAsync(id, ct);
        return !string.IsNullOrEmpty(result.error?.Message)
            ? TypedResults.NotFound(new { error = result.error.Message })
            : TypedResults.Ok(true);
    }

    private static string? ValidateHtmlEditorImage(IFormFile? file) { /* unchanged — same as before */ if (file is null) return "No file was uploaded."; if (file.Length <= 0) return "Uploaded file is empty."; if (file.Length > HtmlEditorImageMaxBytes) return "Uploaded image exceeds the 10 MB limit."; if (!HtmlEditorImageMimeTypes.ContainsKey(file.ContentType)) return "Only JPEG, PNG, WebP, and GIF images are allowed."; var ext = Path.GetExtension(file.FileName); if (string.IsNullOrWhiteSpace(ext)) return "Uploaded image must have a file extension."; var expected = GetSafeImageExtension(file.ContentType, file.FileName); if (!string.Equals(ext, expected, StringComparison.OrdinalIgnoreCase) && !(string.Equals(file.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase) && string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase))) return "Uploaded image extension does not match the content type."; return null; }
    private static string GetSafeImageExtension(string contentType, string fileName) { if (string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase) && string.Equals(Path.GetExtension(fileName), ".jpeg", StringComparison.OrdinalIgnoreCase)) return ".jpeg"; return HtmlEditorImageMimeTypes[contentType]; }
    private static string SanitizeFileName(string value) { var invalid = Path.GetInvalidFileNameChars(); var chars = value.Where(ch => !invalid.Contains(ch)).Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-').ToArray(); var s = new string(chars).Trim('-'); return string.IsNullOrWhiteSpace(s) ? "html-editor-image" : s; }
    private static string GetWebRootPath(Microsoft.AspNetCore.Hosting.IWebHostEnvironment env) => string.IsNullOrWhiteSpace(env.WebRootPath) ? Path.Combine(env.ContentRootPath, "wwwroot") : env.WebRootPath;
}
