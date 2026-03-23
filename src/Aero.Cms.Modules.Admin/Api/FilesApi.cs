namespace Aero.Cms.Modules.Admin.Api;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Admin API for file management.
/// </summary>
public static class FilesApi
{
    /// <summary>
    /// Maps the Files Admin API endpoints.
    /// </summary>
    public static void MapFilesApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/files", GetAllFiles)
            .WithName("GetAllFiles")
            .WithTags("Admin - Files");

        app.MapGet("/api/v1/admin/files/{id:long}", GetFileById)
            .WithName("GetFileById")
            .WithTags("Admin - Files");

        app.MapPost("/api/v1/admin/files", UploadFile)
            .WithName("UploadFile")
            .WithTags("Admin - Files");

        app.MapPut("/api/v1/admin/files/{id:long}", UpdateFile)
            .WithName("UpdateFile")
            .WithTags("Admin - Files");

        app.MapDelete("/api/v1/admin/files/{id:long}", DeleteFile)
            .WithName("DeleteFile")
            .WithTags("Admin - Files");
    }

    private static async Task<IResult> GetAllFiles(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("FilesApi.GetAllFiles is not yet implemented");
    }

    private static async Task<IResult> GetFileById(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"FilesApi.GetFileById({id}) is not yet implemented");
    }

    private static async Task<IResult> UploadFile(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("FilesApi.UploadFile is not yet implemented");
    }

    private static async Task<IResult> UpdateFile(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"FilesApi.UpdateFile({id}) is not yet implemented");
    }

    private static async Task<IResult> DeleteFile(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"FilesApi.DeleteFile({id}) is not yet implemented");
    }
}
