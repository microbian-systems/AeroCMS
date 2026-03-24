using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Headless.Api.v1;

/// <summary>
/// Admin API for user profile management.
/// </summary>
public static class ProfileApi
{
    /// <summary>
    /// Maps the Profile Admin API endpoints.
    /// </summary>
    public static void MapProfileApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/profile", GetProfile)
            .WithName("GetProfile")
            .WithTags("Admin - Profile");

        app.MapPut("/api/v1/admin/profile", UpdateProfile)
            .WithName("UpdateProfile")
            .WithTags("Admin - Profile");

        app.MapPut("/api/v1/admin/profile/password", UpdatePassword)
            .WithName("UpdatePassword")
            .WithTags("Admin - Profile");
    }

    private static async Task<IResult> GetProfile(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("ProfileApi.GetProfile is not yet implemented");
    }

    private static async Task<IResult> UpdateProfile(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("ProfileApi.UpdateProfile is not yet implemented");
    }

    private static async Task<IResult> UpdatePassword(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("ProfileApi.UpdatePassword is not yet implemented");
    }
}
