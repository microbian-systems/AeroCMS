using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Headless.Api.v1;

/// <summary>
/// Admin API for tag management.
/// </summary>
public static class TagsApi
{
    /// <summary>
    /// Maps the Tags Admin API endpoints.
    /// </summary>
    public static void MapTagsApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/tags", GetAllTags)
            .WithName("GetAllTags")
            .WithTags("Admin - Tags");

        app.MapGet("/api/v1/admin/tags/{id:long}", GetTagById)
            .WithName("GetTagById")
            .WithTags("Admin - Tags");

        app.MapPost("/api/v1/admin/tags", CreateTag)
            .WithName("CreateTag")
            .WithTags("Admin - Tags");

        app.MapPut("/api/v1/admin/tags/{id:long}", UpdateTag)
            .WithName("UpdateTag")
            .WithTags("Admin - Tags");

        app.MapDelete("/api/v1/admin/tags/{id:long}", DeleteTag)
            .WithName("DeleteTag")
            .WithTags("Admin - Tags");
    }

    private static async Task<IResult> GetAllTags(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("TagsApi.GetAllTags is not yet implemented");
    }

    private static async Task<IResult> GetTagById(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"TagsApi.GetTagById({id}) is not yet implemented");
    }

    private static async Task<IResult> CreateTag(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("TagsApi.CreateTag is not yet implemented");
    }

    private static async Task<IResult> UpdateTag(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"TagsApi.UpdateTag({id}) is not yet implemented");
    }

    private static async Task<IResult> DeleteTag(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"TagsApi.DeleteTag({id}) is not yet implemented");
    }
}
