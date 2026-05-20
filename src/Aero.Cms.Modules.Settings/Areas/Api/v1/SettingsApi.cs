using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Settings.Areas.Api.v1;

/// <summary>
/// Thin admin API for settings management.
/// Handles input validation and delegates all logic to <see cref="IAeroSettingActor"/> (Orleans grain).
/// </summary>
public static class SettingsApi
{
    public static void MapSettingsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/settings")
            .WithTags("Admin - Settings");

        group.MapGet("/", GetAllSettings)
            .WithName("GetAllSettings");

        group.MapGet("/key/{key}", GetSettingByKey)
            .WithName("GetSettingByKey");

        group.MapGet("/category/{category}", GetSettingsByCategory)
            .WithName("GetSettingsByCategory");

        group.MapPost("/", SetSetting)
            .WithName("SetSetting");

        group.MapDelete("/key/{key}", DeleteSetting)
            .WithName("DeleteSetting");

        group.MapGet("/categories", GetCategories)
            .WithName("GetSettingCategories");
    }

    private static async Task<IResult> GetAllSettings(
        [FromServices] IAeroSettingActor settingActor,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingActor.GetAllAsync(cancellationToken);
        return TypedResults.Ok(settings);
    }

    private static async Task<IResult> GetSettingByKey(
        string key,
        [FromServices] IAeroSettingActor settingActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(SettingsApi));
        logger.LogDebug("Getting setting {Key}", key);
        var detail = await settingActor.GetByKeyAsync(key, cancellationToken);

        if (detail is null)
            return TypedResults.NotFound(new { error = $"Setting with key '{key}' not found." });

        return TypedResults.Ok(detail);
    }

    private static async Task<IResult> GetSettingsByCategory(
        string category,
        [FromServices] IAeroSettingActor settingActor,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingActor.GetByCategoryAsync(category, cancellationToken);
        return TypedResults.Ok(settings);
    }

    private static async Task<IResult> SetSetting(
        [FromBody] SetSettingRequest request,
        [FromServices] IAeroSettingActor settingActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(SettingsApi));
        logger.LogDebug("Setting {Key} = {Value}", request.Key, request.Value);
        var detail = await settingActor.SetAsync(request.Key, request.Value, request.Category, request.Type, cancellationToken);
        return TypedResults.Ok(detail);
    }

    private static async Task<IResult> DeleteSetting(
        string key,
        [FromServices] IAeroSettingActor settingActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(SettingsApi));
        logger.LogDebug("Deleting setting {Key}", key);
        var deleted = await settingActor.DeleteAsync(key, cancellationToken);

        if (!deleted)
            return TypedResults.NotFound(new { error = $"Setting with key '{key}' not found." });

        return TypedResults.Ok(true);
    }

    private static async Task<IResult> GetCategories(
        [FromServices] IAeroSettingActor settingActor,
        CancellationToken cancellationToken = default)
    {
        var categories = await settingActor.GetCategoriesAsync(cancellationToken);
        return TypedResults.Ok(categories);
    }
}
