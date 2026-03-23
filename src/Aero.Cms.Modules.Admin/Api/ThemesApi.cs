namespace Aero.Cms.Modules.Admin.Api;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Admin API for theme management.
/// </summary>
public static class ThemesApi
{
    /// <summary>
    /// Maps the Themes Admin API endpoints.
    /// </summary>
    public static void MapThemesApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/themes", GetAllThemes)
            .WithName("GetAllThemes")
            .WithTags("Admin - Themes");

        app.MapGet("/api/v1/admin/themes/{id}", GetThemeById)
            .WithName("GetThemeById")
            .WithTags("Admin - Themes");

        app.MapPost("/api/v1/admin/themes", CreateTheme)
            .WithName("CreateTheme")
            .WithTags("Admin - Themes");

        app.MapPut("/api/v1/admin/themes/{id}", UpdateTheme)
            .WithName("UpdateTheme")
            .WithTags("Admin - Themes");

        app.MapDelete("/api/v1/admin/themes/{id}", DeleteTheme)
            .WithName("DeleteTheme")
            .WithTags("Admin - Themes");

        app.MapPost("/api/v1/admin/themes/{id}/activate", ActivateTheme)
            .WithName("ActivateTheme")
            .WithTags("Admin - Themes");
    }

    private static async Task<IResult> GetAllThemes(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("ThemesApi.GetAllThemes is not yet implemented");
    }

    private static async Task<IResult> GetThemeById(string id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"ThemesApi.GetThemeById({id}) is not yet implemented");
    }

    private static async Task<IResult> CreateTheme(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("ThemesApi.CreateTheme is not yet implemented");
    }

    private static async Task<IResult> UpdateTheme(string id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"ThemesApi.UpdateTheme({id}) is not yet implemented");
    }

    private static async Task<IResult> DeleteTheme(string id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"ThemesApi.DeleteTheme({id}) is not yet implemented");
    }

    private static async Task<IResult> ActivateTheme(string id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"ThemesApi.ActivateTheme({id}) is not yet implemented");
    }
}
