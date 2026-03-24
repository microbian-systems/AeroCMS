using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Headless.Api.v1;

/// <summary>
/// Admin API for module management.
/// </summary>
public static class ModulesApi
{
    /// <summary>
    /// Maps the Modules Admin API endpoints.
    /// </summary>
    public static void MapModulesApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/modules", GetAllModules)
            .WithName("GetAllModules")
            .WithTags("Admin - Modules");

        app.MapGet("/api/v1/admin/modules/{id}", GetModuleById)
            .WithName("GetModuleById")
            .WithTags("Admin - Modules");

        app.MapPost("/api/v1/admin/modules", CreateModule)
            .WithName("CreateModule")
            .WithTags("Admin - Modules");

        app.MapPut("/api/v1/admin/modules/{id}", UpdateModule)
            .WithName("UpdateModule")
            .WithTags("Admin - Modules");

        app.MapDelete("/api/v1/admin/modules/{id}", DeleteModule)
            .WithName("DeleteModule")
            .WithTags("Admin - Modules");
    }

    private static async Task<IResult> GetAllModules(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("ModulesApi.GetAllModules is not yet implemented");
    }

    private static async Task<IResult> GetModuleById(string id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"ModulesApi.GetModuleById({id}) is not yet implemented");
    }

    private static async Task<IResult> CreateModule(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("ModulesApi.CreateModule is not yet implemented");
    }

    private static async Task<IResult> UpdateModule(string id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"ModulesApi.UpdateModule({id}) is not yet implemented");
    }

    private static async Task<IResult> DeleteModule(string id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"ModulesApi.DeleteModule({id}) is not yet implemented");
    }
}
