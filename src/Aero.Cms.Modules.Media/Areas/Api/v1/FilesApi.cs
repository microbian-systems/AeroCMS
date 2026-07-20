using Aero.Cms.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Media.Areas.Api.v1;

/// <summary>
/// Maps administrative CRUD endpoints for database-backed general files.
/// </summary>
/// <remarks>
/// The handlers do not apply tenant/site filters or validate file paths and content. They also
/// return caught exception messages in problem responses. The host must enforce the admin
/// authorization boundary and treat request and response content according to its trust policy.
/// </remarks>
public static class FilesApi
{
    /// <summary>
    /// Maps the general-file endpoints beneath the versioned admin route.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <remarks>This method does not attach an authorization policy to the route group.</remarks>
    public static void MapFilesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/files")
            .WithTags("Admin - Files");

        group.MapGet("/", GetAllFiles)
            .WithName("GetAllFiles");

        group.MapGet("/details/{id:long}", GetFileById)
            .WithName("GetFileById");

        group.MapPost("/", UploadFile)
            .WithName("UploadFile");

        group.MapDelete("/{id:long}", DeleteFile)
            .WithName("DeleteFile");

        group.MapPost("/{id:long}/move", MoveFile)
            .WithName("MoveFile");
    }

    /// <summary>
    /// Lists all files, optionally filtering by a case-sensitive path prefix.
    /// </summary>
    /// <returns>An HTTP 200 summary list, or a problem response containing a caught exception message.</returns>
    /// <remarks>Cancellation is caught and converted to a problem response.</remarks>
    private static async Task<IResult> GetAllFiles(
        [FromQuery] string? folder,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(FilesApi));
        try
        {
            IQueryable<CmsFile> query = session.Query<CmsFile>();

            if (!string.IsNullOrEmpty(folder))
            {
                query = query.Where(x => x.Path.StartsWith(folder));
            }

            var files = await query
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            var summaries = files.Select(f => new FileSummary(
                f.Id,
                f.Name,
                f.Path,
                f.Size,
                f.CreatedOn.DateTime,
                f.ModifiedOn.GetValueOrDefault().DateTime
            )).ToList();

            return TypedResults.Ok(summaries);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving all files");
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Loads a file document by identifier and returns its stored content.
    /// </summary>
    /// <returns>An HTTP 200 detail, 404 when absent, or a problem response on failure.</returns>
    /// <remarks>No site-ownership check is performed before returning the raw stored content.</remarks>
    private static async Task<IResult> GetFileById(
        long id,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(FilesApi));
        try
        {
            var file = await session.LoadAsync<CmsFile>(id, cancellationToken);

            if (file is null)
            {
                return TypedResults.NotFound(new { error = $"File with ID {id} not found." });
            }

            var detail = new FileDetail(
                file.Id,
                file.Name,
                file.Path,
                file.Size,
                file.MimeType,
                file.CreatedOn.DateTime,
                file.ModifiedOn.GetValueOrDefault().DateTime,
                file.Content
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving file for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Creates and commits a file document from the supplied metadata and content.
    /// </summary>
    /// <returns>An HTTP 200 detail for the committed document, or a problem response on failure.</returns>
    /// <remarks>
    /// The folder and name are joined with slash collapsing only; containment, uniqueness, size,
    /// media type, and content are not independently validated by this handler.
    /// </remarks>
    private static async Task<IResult> UploadFile(
        [FromBody] UploadFileRequest request,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(FilesApi));
        try
        {
            var file = new CmsFile
            {
                Id = Snowflake.NewId(),
                Name = request.Name,
                Path = $"{request.Folder}/{request.Name}".Replace("//", "/"),
                Size = request.Size,
                MimeType = request.MimeType,
                Content = request.Content
            };

            session.Store(file);
            await session.SaveChangesAsync(cancellationToken);

            var detail = new FileDetail(
                file.Id,
                file.Name,
                file.Path,
                file.Size,
                file.MimeType,
                file.CreatedOn.DateTime,
                file.ModifiedOn.GetValueOrDefault().DateTime,
                file.Content
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading file");
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Deletes a file document by identifier and commits the session.
    /// </summary>
    /// <returns>HTTP 200 on deletion, 404 when absent, or a problem response on failure.</returns>
    private static async Task<IResult> DeleteFile(
        long id,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(FilesApi));
        try
        {
            var file = await session.LoadAsync<CmsFile>(id, cancellationToken);

            if (file is null)
            {
                return TypedResults.NotFound(new { error = $"File with ID {id} not found." });
            }

            session.Delete(file);
            await session.SaveChangesAsync(cancellationToken);

            return TypedResults.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting file for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Rewrites a file document's logical path and commits the updated document.
    /// </summary>
    /// <returns>HTTP 200 on success, 404 when absent, or a problem response on failure.</returns>
    /// <remarks>
    /// This updates database metadata only. The folder value receives no containment validation,
    /// and the handler performs no site-ownership check.
    /// </remarks>
    private static async Task<IResult> MoveFile(
        long id,
        [FromQuery] string folder,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(FilesApi));
        try
        {
            var file = await session.LoadAsync<CmsFile>(id, cancellationToken);

            if (file is null)
            {
                return TypedResults.NotFound(new { error = $"File with ID {id} not found." });
            }

            file.Path = $"{folder}/{file.Name}".Replace("//", "/");
            file.ModifiedOn = DateTimeOffset.UtcNow;

            session.Store(file);
            await session.SaveChangesAsync(cancellationToken);

            return TypedResults.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error moving file for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }
}
