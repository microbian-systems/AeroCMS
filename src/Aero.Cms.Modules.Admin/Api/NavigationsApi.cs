namespace Aero.Cms.Modules.Admin.Api;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Admin API for navigation management.
/// </summary>
public static class NavigationsApi
{
    /// <summary>
    /// Maps the Navigations Admin API endpoints.
    /// </summary>
    public static void MapNavigationsApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/navigations", GetAllNavigations)
            .WithName("GetAllNavigations")
            .WithTags("Admin - Navigations");

        app.MapGet("/api/v1/admin/navigations/{id:long}", GetNavigationById)
            .WithName("GetNavigationById")
            .WithTags("Admin - Navigations");

        app.MapPost("/api/v1/admin/navigations", CreateNavigation)
            .WithName("CreateNavigation")
            .WithTags("Admin - Navigations");

        app.MapPut("/api/v1/admin/navigations/{id:long}", UpdateNavigation)
            .WithName("UpdateNavigation")
            .WithTags("Admin - Navigations");

        app.MapDelete("/api/v1/admin/navigations/{id:long}", DeleteNavigation)
            .WithName("DeleteNavigation")
            .WithTags("Admin - Navigations");
    }

    private static async Task<IResult> GetAllNavigations(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("NavigationsApi.GetAllNavigations is not yet implemented");
    }

    private static async Task<IResult> GetNavigationById(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"NavigationsApi.GetNavigationById({id}) is not yet implemented");
    }

    private static async Task<IResult> CreateNavigation(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("NavigationsApi.CreateNavigation is not yet implemented");
    }

    private static async Task<IResult> UpdateNavigation(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"NavigationsApi.UpdateNavigation({id}) is not yet implemented");
    }

    private static async Task<IResult> DeleteNavigation(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"NavigationsApi.DeleteNavigation({id}) is not yet implemented");
    }
}
