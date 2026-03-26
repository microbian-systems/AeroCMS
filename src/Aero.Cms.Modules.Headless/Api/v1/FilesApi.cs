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
/// Admin API for general file management.
/// </summary>
public static class FilesApi
{
    /// <summary>
    /// Maps the Files Admin API endpoints.
    /// </summary>
    public static void MapFilesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/files")
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
