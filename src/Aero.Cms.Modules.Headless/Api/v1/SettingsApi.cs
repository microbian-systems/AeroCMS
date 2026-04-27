using Aero.Cms.Core;
using Aero.Cms.Abstractions.Http.Clients;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Headless.Api.v1;

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
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(SettingsApi));
        try
        {
            var settings = await session.Query<Setting>()
                .OrderBy(x => x.Category)
                .ThenBy(x => x.Key)
                .ToListAsync(cancellationToken);

            var summaries = settings.Select(s => new SettingSummary(
                s.Key,
                s.Category,
                s.Description
            )).ToList();

            return TypedResults.Ok(summaries);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving all settings");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetSettingByKey(
        string key,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(SettingsApi));
        try
        {
            var setting = await session.LoadAsync<Setting>(key, cancellationToken);

            if (setting is null)
            {
                return TypedResults.NotFound(new { error = $"Setting with key '{key}' not found." });
            }

            var detail = new SettingDetail(
                setting.Key,
                setting.Value,
                setting.Category,
                setting.Description,
                setting.Type,
                setting.ModifiedOn.GetValueOrDefault().DateTime
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving setting for key={Key}", key);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetSettingsByCategory(
        string category,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(SettingsApi));
        try
        {
            var settings = await session.Query<Setting>()
                .Where(x => x.Category == category)
                .OrderBy(x => x.Key)
                .ToListAsync(cancellationToken);

            var details = settings.Select(s => new SettingDetail(
                s.Key,
                s.Value,
                s.Category,
                s.Description,
                s.Type,
                s.ModifiedOn.GetValueOrDefault().DateTime
            )).ToList();

            return TypedResults.Ok(details);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving settings for category={Category}", category);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> SetSetting(
        [FromBody] SetSettingRequest request,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(SettingsApi));
        try
        {
            var setting = await session.LoadAsync<Setting>(request.Key, cancellationToken);

            if (setting is null)
            {
                setting = new Setting
                {
                    Key = request.Key,
                    Category = request.Category,
                    Type = request.Type
                };
            }

            setting.Value = request.Value;
            setting.Category = request.Category;
            setting.Type = request.Type;
            setting.ModifiedOn = DateTimeOffset.UtcNow;

            session.Store(setting);
            await session.SaveChangesAsync(cancellationToken);

            var detail = new SettingDetail(
                setting.Key,
                setting.Value,
                setting.Category,
                setting.Description,
                setting.Type,
                setting.ModifiedOn.GetValueOrDefault().DateTime
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting value for key={Key}", request.Key);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> DeleteSetting(
        string key,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(SettingsApi));
        try
        {
            var setting = await session.LoadAsync<Setting>(key, cancellationToken);

            if (setting is null)
            {
                return TypedResults.NotFound(new { error = $"Setting with key '{key}' not found." });
            }

            session.Delete(setting);
            await session.SaveChangesAsync(cancellationToken);

            return TypedResults.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting setting for key={Key}", key);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetCategories(
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(SettingsApi));
        try
        {
            var categories = await session.Query<Setting>()
                .GroupBy(x => x.Category)
                .Select(g => new SettingCategory(g.Key, g.Count()))
                .ToListAsync(cancellationToken);

            return TypedResults.Ok(categories);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving setting categories");
            return TypedResults.Problem(ex.Message);
        }
    }
}
