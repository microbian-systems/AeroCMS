namespace Aero.Cms.Modules.Admin.Api;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Admin API for settings management.
/// </summary>
public static class SettingsApi
{
    /// <summary>
    /// Maps the Settings Admin API endpoints.
    /// </summary>
    public static void MapSettingsApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/settings", GetAllSettings)
            .WithName("GetAllSettings")
            .WithTags("Admin - Settings");

        app.MapGet("/api/v1/admin/settings/{key}", GetSettingByKey)
            .WithName("GetSettingByKey")
            .WithTags("Admin - Settings");

        app.MapPut("/api/v1/admin/settings/{key}", UpdateSetting)
            .WithName("UpdateSetting")
            .WithTags("Admin - Settings");

        app.MapDelete("/api/v1/admin/settings/{key}", DeleteSetting)
            .WithName("DeleteSetting")
            .WithTags("Admin - Settings");
    }

    private static async Task<IResult> GetAllSettings(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("SettingsApi.GetAllSettings is not yet implemented");
    }

    private static async Task<IResult> GetSettingByKey(string key, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"SettingsApi.GetSettingByKey({key}) is not yet implemented");
    }

    private static async Task<IResult> UpdateSetting(string key, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"SettingsApi.UpdateSetting({key}) is not yet implemented");
    }

    private static async Task<IResult> DeleteSetting(string key, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"SettingsApi.DeleteSetting({key}) is not yet implemented");
    }
}
