using Aero.Cms.Abstractions.Theming;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Theming.Areas.Api.v1;

/// <summary>Maps read-only administrative discovery for deployment-installed themes.</summary>
public static class ThemesApi
{
    /// <summary>Maps the immutable deployment theme catalog endpoints.</summary>
    public static void MapThemesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/themes")
            .WithTags("Admin - Themes")
            .RequireAuthorization();

        group.MapGet("/", GetAllThemes).WithName("GetAllThemes");
        group.MapGet("/details/{id}/{version}", GetTheme).WithName("GetThemeByIdentity");
    }

    private static IResult GetAllThemes([FromServices] IThemeCatalog catalog)
        => TypedResults.Ok<IReadOnlyList<ThemeSummary>>(
            catalog.GetAll().Select(ToSummary).ToList());

    private static IResult GetTheme(string id, string version, [FromServices] IThemeCatalog catalog)
    {
        var theme = catalog.Find(id, version);
        return theme is null ? TypedResults.NotFound() : TypedResults.Ok(ToDetail(theme));
    }

    private static ThemeSummary ToSummary(InstalledThemeManifest manifest)
        => new(manifest.Id, manifest.Name, manifest.Version, manifest.Author, manifest.ThumbnailUrl, manifest.IsSafeDefault);

    private static ThemeDetail ToDetail(InstalledThemeManifest manifest)
        => new(
            manifest.Id,
            manifest.Name,
            manifest.Version,
            manifest.Author,
            manifest.Description,
            manifest.ThumbnailUrl,
            manifest.IsSafeDefault,
            manifest.Stylesheets.Select(static asset => new ThemeAsset(asset.Path, "stylesheet")).ToList());
}
