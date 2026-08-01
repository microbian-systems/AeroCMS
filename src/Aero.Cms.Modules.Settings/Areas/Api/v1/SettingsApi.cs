using Aero.Cms.Abstractions.Actors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Settings.Areas.Api.v1;

/// <summary>
/// Maps HTTP endpoints for settings management and delegates persistence to
/// <see cref="IAeroSettingActor"/>.
/// </summary>
/// <remarks>
/// The route group requires the <c>AeroAdmin</c> policy because values are returned and mutated
/// without site or tenant scoping.
/// </remarks>
public static class SettingsApi
{
        /// <summary>
    /// Maps settings query and mutation endpoints under the administrative API prefix.
    /// </summary>
    /// <param name="app">The endpoint route builder that receives the settings routes.</param>
public static void MapSettingsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/settings")
            .WithTags("Admin - Settings")
            .RequireAuthorization("AeroAdmin");

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

    /// <summary>
    /// Returns summaries for every stored setting.
    /// </summary>
    /// <param name="settingActor">The actor that reads setting documents.</param>
    /// <param name="cancellationToken">The token propagated to the actor call.</param>
    /// <returns>An HTTP 200 result containing the actor's complete setting list.</returns>
    private static async Task<IResult> GetAllSettings(
        [FromServices] IAeroSettingActor settingActor,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingActor.GetAllAsync(cancellationToken);
        return TypedResults.Ok(settings);
    }

    /// <summary>
    /// Looks up one setting by its case-sensitive storage key.
    /// </summary>
    /// <returns>HTTP 200 with the setting, or HTTP 404 when the key is absent.</returns>
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

    /// <summary>
    /// Returns settings whose stored category exactly matches the route value.
    /// </summary>
    /// <returns>An HTTP 200 result; an unmatched category produces an empty collection.</returns>
    private static async Task<IResult> GetSettingsByCategory(
        string category,
        [FromServices] IAeroSettingActor settingActor,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingActor.GetByCategoryAsync(category, cancellationToken);
        return TypedResults.Ok(settings);
    }

    /// <summary>
    /// Creates or replaces a setting value through the backing actor.
    /// </summary>
    /// <returns>HTTP 200 with the persisted setting detail.</returns>
    /// <remarks>
    /// The endpoint performs no local validation and logs the submitted value at debug level.
    /// Persistence and cancellation exceptions propagate to the ASP.NET Core pipeline.
    /// </remarks>
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

    /// <summary>
    /// Deletes the setting identified by the route key.
    /// </summary>
    /// <returns>HTTP 200 with <see langword="true"/> when deleted, or HTTP 404 when absent.</returns>
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

    /// <summary>
    /// Returns category names and counts derived from all settings.
    /// </summary>
    /// <returns>An HTTP 200 result containing the category aggregates.</returns>
    private static async Task<IResult> GetCategories(
        [FromServices] IAeroSettingActor settingActor,
        CancellationToken cancellationToken = default)
    {
        var categories = await settingActor.GetCategoriesAsync(cancellationToken);
        return TypedResults.Ok(categories);
    }
}
