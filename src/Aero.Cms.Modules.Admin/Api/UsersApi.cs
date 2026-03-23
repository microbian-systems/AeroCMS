namespace Aero.Cms.Modules.Admin.Api;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Admin API for user management.
/// </summary>
public static class UsersApi
{
    /// <summary>
    /// Maps the Users Admin API endpoints.
    /// </summary>
    public static void MapUsersApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/users", GetAllUsers)
            .WithName("GetAllUsers")
            .WithTags("Admin - Users");

        app.MapGet("/api/v1/admin/users/{id:long}", GetUserById)
            .WithName("GetUserById")
            .WithTags("Admin - Users");

        app.MapPost("/api/v1/admin/users", CreateUser)
            .WithName("CreateUser")
            .WithTags("Admin - Users");

        app.MapPut("/api/v1/admin/users/{id:long}", UpdateUser)
            .WithName("UpdateUser")
            .WithTags("Admin - Users");

        app.MapDelete("/api/v1/admin/users/{id:long}", DeleteUser)
            .WithName("DeleteUser")
            .WithTags("Admin - Users");
    }

    private static async Task<IResult> GetAllUsers(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("UsersApi.GetAllUsers is not yet implemented");
    }

    private static async Task<IResult> GetUserById(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"UsersApi.GetUserById({id}) is not yet implemented");
    }

    private static async Task<IResult> CreateUser(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("UsersApi.CreateUser is not yet implemented");
    }

    private static async Task<IResult> UpdateUser(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"UsersApi.UpdateUser({id}) is not yet implemented");
    }

    private static async Task<IResult> DeleteUser(long id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"UsersApi.DeleteUser({id}) is not yet implemented");
    }
}
