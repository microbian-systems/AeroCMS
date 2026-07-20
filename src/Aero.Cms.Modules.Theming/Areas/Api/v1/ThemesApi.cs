using Aero.Cms.Core.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Theming.Areas.Api.v1;

/// <summary>
/// Maps administrative theme discovery and currently unimplemented mutation operations.
/// </summary>
/// <remarks>
/// The route mapper does not attach authorization. The host must secure the administrative group.
/// Active-theme state is currently hard-coded rather than persisted. Unexpected exception
/// messages are copied into problem responses after logging.
/// </remarks>
public static class ThemesApi
{
    /// <summary>
    /// Maps the administrative themes endpoint group.
    /// </summary>
    /// <param name="app">The endpoint route builder receiving the group.</param>
    public static void MapThemesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/themes")
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

    /// <summary>
    /// Enumerates registered theme modules and marks only the module named <c>Default</c> as active.
    /// </summary>
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

    /// <summary>
    /// Finds a registered theme by case-sensitive name and projects transient detail metadata.
    /// </summary>
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

    /// <summary>
    /// Returns the registered <c>Default</c> theme or falls back to the first discovered module.
    /// </summary>
    /// <remarks>The returned detail is marked active even when the fallback is not named <c>Default</c>.</remarks>
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

    /// <summary>
    /// Verifies that a theme exists and returns it as active without persisting any selection.
    /// </summary>
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

    /// <summary>
    /// Returns a problem response because dynamic theme upload is not implemented.
    /// </summary>
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

    /// <summary>
    /// Returns a problem response because theme deletion is not implemented.
    /// </summary>
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
