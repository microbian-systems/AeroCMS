using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Headless.Api.v1;

/// <summary>
/// Admin API for media management.
/// </summary>
public static class MediaApi
{
    /// <summary>
    /// Maps the Media Admin API endpoints.
    /// </summary>
    public static void MapMediaApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/media", GetAllMedia)
            .WithName("GetAllMedia")
            .WithTags("Admin - Media");

        app.MapGet("/api/v1/admin/media/{id:long}", GetMediaById)
            .WithName("GetMediaById")
            .WithTags("Admin - Media");

        app.MapPost("/api/v1/admin/media", CreateMedia)
            .WithName("CreateMedia")
            .WithTags("Admin - Media");

        app.MapPut("/api/v1/admin/media/{id:long}", UpdateMedia)
            .WithName("UpdateMedia")
            .WithTags("Admin - Media");

        app.MapDelete("/api/v1/admin/media/{id:long}", DeleteMedia)
            .WithName("DeleteMedia")
            .WithTags("Admin - Media");
    }

    private static async Task<IResult> GetAllMedia(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("MediaApi.GetAllMedia is not yet implemented");
    }

    private static async Task<IResult> GetMediaById(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"MediaApi.GetMediaById({id}) is not yet implemented");
    }

    private static async Task<IResult> CreateMedia(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("MediaApi.CreateMedia is not yet implemented");
    }

    private static async Task<IResult> UpdateMedia(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"MediaApi.UpdateMedia({id}) is not yet implemented");
    }

    private static async Task<IResult> DeleteMedia(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"MediaApi.DeleteMedia({id}) is not yet implemented");
    }
}
