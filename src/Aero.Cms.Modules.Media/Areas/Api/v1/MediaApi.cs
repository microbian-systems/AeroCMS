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
/// <remarks>
/// These handlers do not add authorization or site-ownership checks. The host must protect the
/// admin route group and constrain actor operations to the authorized site.
/// </remarks>
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
    /// Maps media browsing, metadata, deletion, and upload endpoints beneath the admin route.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <remarks>
    /// The HTML-editor upload explicitly disables antiforgery, and this method does not attach an
    /// authorization policy. The surrounding host pipeline must supply the intended protections.
    /// </remarks>
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

    /// <summary>
    /// Returns a requested page of actor-provided media summaries.
    /// </summary>
    /// <returns>An HTTP 200 paged result; actor and cancellation exceptions propagate.</returns>
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

    /// <summary>
    /// Returns media details for an identifier.
    /// </summary>
    /// <returns>HTTP 200 on success or 404 when the actor response contains an error message.</returns>
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

    /// <summary>
    /// Persists folder metadata through the media actor.
    /// </summary>
    /// <returns>An HTTP 200 detail built from the actor response.</returns>
    /// <remarks>
    /// The unused local <see cref="MediaAsset"/> is not persisted. An actor error is not checked
    /// before the response data is dereferenced.
    /// </remarks>
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

    /// <summary>
    /// Optionally writes decoded Base64 bytes to the web root, then persists media metadata.
    /// </summary>
    /// <returns>An HTTP 200 detail built from the actor response.</returns>
    /// <remarks>
    /// The request file name is combined directly with the media directory without sanitization
    /// or containment validation. Hosts must validate trusted file names and payload sizes before
    /// this handler. Disk write and actor persistence are not transactional: a write can remain
    /// after persistence failure, and actor errors are not checked before response construction.
    /// </remarks>
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

    /// <summary>
    /// Validates and stores a multipart image for HTML-editor use, then persists its metadata.
    /// </summary>
    /// <returns>HTTP 200 with URL and identifier, or HTTP 400 for form and metadata validation failures.</returns>
    /// <remarks>
    /// Validation trusts the declared MIME type and extension rather than inspecting file bytes.
    /// The disk write occurs before actor persistence and is not rolled back when persistence fails;
    /// actor error responses are not checked.
    /// </remarks>
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

    /// <summary>
    /// Updates the file name, title, alternate text, and description through the actor.
    /// </summary>
    /// <returns>HTTP 200 on success or 404 when the actor response contains an error message.</returns>
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

    /// <summary>
    /// Deletes media metadata through the actor.
    /// </summary>
    /// <returns>HTTP 200 on success or 404 when the actor response contains an error message.</returns>
    /// <remarks>This handler does not remove a corresponding physical media file.</remarks>
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

    /// <summary>
    /// Validates presence, size, declared MIME type, and extension consistency for an editor upload.
    /// </summary>
    /// <param name="file">The uploaded file, if supplied.</param>
    /// <returns>An error message when invalid; otherwise, <see langword="null"/>.</returns>
    private static string? ValidateHtmlEditorImage(IFormFile? file) { /* unchanged — same as before */ if (file is null) return "No file was uploaded."; if (file.Length <= 0) return "Uploaded file is empty."; if (file.Length > HtmlEditorImageMaxBytes) return "Uploaded image exceeds the 10 MB limit."; if (!HtmlEditorImageMimeTypes.ContainsKey(file.ContentType)) return "Only JPEG, PNG, WebP, and GIF images are allowed."; var ext = Path.GetExtension(file.FileName); if (string.IsNullOrWhiteSpace(ext)) return "Uploaded image must have a file extension."; var expected = GetSafeImageExtension(file.ContentType, file.FileName); if (!string.Equals(ext, expected, StringComparison.OrdinalIgnoreCase) && !(string.Equals(file.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase) && string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase))) return "Uploaded image extension does not match the content type."; return null; }
    /// <summary>
    /// Selects the allowlisted storage extension for a declared image MIME type.
    /// </summary>
    /// <returns><c>.jpeg</c> for a JPEG upload that used that extension; otherwise, the allowlisted extension.</returns>
    private static string GetSafeImageExtension(string contentType, string fileName) { if (string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase) && string.Equals(Path.GetExtension(fileName), ".jpeg", StringComparison.OrdinalIgnoreCase)) return ".jpeg"; return HtmlEditorImageMimeTypes[contentType]; }
    /// <summary>
    /// Converts a source file stem to an allowlisted alphanumeric, dash, and underscore form.
    /// </summary>
    /// <returns>A safe stem, or <c>html-editor-image</c> when no characters remain.</returns>
    private static string SanitizeFileName(string value) { var invalid = Path.GetInvalidFileNameChars(); var chars = value.Where(ch => !invalid.Contains(ch)).Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-').ToArray(); var s = new string(chars).Trim('-'); return string.IsNullOrWhiteSpace(s) ? "html-editor-image" : s; }
    /// <summary>
    /// Resolves the configured web root or falls back to <c>wwwroot</c> below the content root.
    /// </summary>
    /// <returns>The effective web-root path.</returns>
    private static string GetWebRootPath(Microsoft.AspNetCore.Hosting.IWebHostEnvironment env) => string.IsNullOrWhiteSpace(env.WebRootPath) ? Path.Combine(env.ContentRootPath, "wwwroot") : env.WebRootPath;
}
