using Aero.Cms.Core.Extensions;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Web.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Headless.Api.v1;

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
        var group = app.MapGroup("/api/v1/admin/themes")
            .WithTags("Admin - Themes");

        group.MapGet("/", GetAllThemes)
            .WithName("GetAllThemes");

        group.MapGet("/details/{id}", GetThemeById)
            .WithName("GetThemeById");

        group.MapGet("/current", GetCurrentTheme)
            .WithName("GetCurrentTheme");

        group.MapPost("/{id}/activate", ActivateTheme)
            .WithName("ActivateTheme");

        group.MapPost("/", UploadTheme)
            .WithName("UploadTheme");

        group.MapDelete("/{id}", DeleteTheme)
            .WithName("DeleteTheme");
    }

    private static async Task<IResult> GetAllThemes(
        [FromServices] IServiceProvider sp,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ThemesApi));
        try
        {
            var themes = sp.GetThemeModules().ToList();
            var activeThemeName = "Default"; // TODO: Get from settings

            var summaries = themes.Select(t => new ThemeSummary(
                t.Name,
                t.Name,
                t.Version,
                t.Author,
                null,
                t.Name == activeThemeName
            )).ToList();

            return TypedResults.Ok(summaries);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving all themes");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetThemeById(
        string id,
        [FromServices] IServiceProvider sp,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ThemesApi));
        try
        {
            var theme = sp.GetThemeModules().FirstOrDefault(t => t.Name == id);

            if (theme is null)
            {
                return TypedResults.NotFound(new { error = $"Theme with ID '{id}' not found." });
            }

            var activeThemeName = "Default"; // TODO: Get from settings

            var detail = new ThemeDetail(
                theme.Name,
                theme.Name,
                theme.Version,
                theme.Author,
                theme.Description ?? string.Empty,
                null,
                theme.Name == activeThemeName,
                [],
                DateTime.UtcNow
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving theme for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetCurrentTheme(
        [FromServices] IServiceProvider sp,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ThemesApi));
        try
        {
            var activeThemeName = "Default"; // TODO: Get from settings
            var theme = sp.GetThemeModules().FirstOrDefault(t => t.Name == activeThemeName) 
                        ?? sp.GetThemeModules().FirstOrDefault();

            if (theme is null)
            {
                return TypedResults.NotFound(new { error = "No themes found." });
            }

            var detail = new ThemeDetail(
                theme.Name,
                theme.Name,
                theme.Version,
                theme.Author,
                theme.Description ?? string.Empty,
                null,
                true,
                [],
                DateTime.UtcNow
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving current theme");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> ActivateTheme(
        string id,
        [FromServices] IServiceProvider sp,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ThemesApi));
        try
        {
            var theme = sp.GetThemeModules().FirstOrDefault(t => t.Name == id);

            if (theme is null)
            {
                return TypedResults.NotFound(new { error = $"Theme with ID '{id}' not found." });
            }

            // TODO: Save active theme to settings
            
            var detail = new ThemeDetail(
                theme.Name,
                theme.Name,
                theme.Version,
                theme.Author,
                theme.Description ?? string.Empty,
                null,
                true,
                [],
                DateTime.UtcNow
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error activating theme id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UploadTheme(
        [FromBody] UploadThemeRequest request,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ThemesApi));
        try
        {
            // In a modular system, uploading a theme might involve saving a ZIP and restarting or dynamic loading
            return TypedResults.Problem("Theme upload not implemented.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading theme");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> DeleteTheme(
        string id,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(ThemesApi));
        try
        {
            return TypedResults.Problem("Theme deletion not implemented.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting theme id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }
}
