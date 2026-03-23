namespace Aero.Cms.Modules.Admin.Api;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Admin API for category management.
/// </summary>
public static class CategoriesApi
{
    /// <summary>
    /// Maps the Categories Admin API endpoints.
    /// </summary>
    public static void MapCategoriesApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/categories", GetAllCategories)
            .WithName("GetAllCategories")
            .WithTags("Admin - Categories");

        app.MapGet("/api/v1/admin/categories/{id:long}", GetCategoryById)
            .WithName("GetCategoryById")
            .WithTags("Admin - Categories");

        app.MapPost("/api/v1/admin/categories", CreateCategory)
            .WithName("CreateCategory")
            .WithTags("Admin - Categories");

        app.MapPut("/api/v1/admin/categories/{id:long}", UpdateCategory)
            .WithName("UpdateCategory")
            .WithTags("Admin - Categories");

        app.MapDelete("/api/v1/admin/categories/{id:long}", DeleteCategory)
            .WithName("DeleteCategory")
            .WithTags("Admin - Categories");
    }

    private static async Task<IResult> GetAllCategories(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("CategoriesApi.GetAllCategories is not yet implemented");
    }

    private static async Task<IResult> GetCategoryById(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"CategoriesApi.GetCategoryById({id}) is not yet implemented");
    }

    private static async Task<IResult> CreateCategory(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("CategoriesApi.CreateCategory is not yet implemented");
    }

    private static async Task<IResult> UpdateCategory(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"CategoriesApi.UpdateCategory({id}) is not yet implemented");
    }

    private static async Task<IResult> DeleteCategory(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"CategoriesApi.DeleteCategory({id}) is not yet implemented");
    }
}
