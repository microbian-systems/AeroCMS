using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Theming;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Theming;

/// <summary>Immutable catalog of exact theme versions installed by the application deployment.</summary>
public sealed class DeploymentThemeCatalog : IThemeCatalog
{
    private readonly IReadOnlyList<InstalledThemeManifest> _manifests;
    private readonly IReadOnlyDictionary<(string Id, string Version), InstalledThemeManifest> _byIdentity;

    /// <summary>Creates and validates an immutable deployment theme catalog.</summary>
    public DeploymentThemeCatalog(IEnumerable<InstalledThemeManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(manifests);
        var validated = manifests.Select(Validate).ToArray();
        if (validated.Length == 0)
            throw new InvalidOperationException("At least one deployment-installed theme is required.");

        var duplicate = validated.GroupBy(static theme => (theme.Id, theme.Version))
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Theme '{duplicate.Key.Id}@{duplicate.Key.Version}' is registered more than once.");

        var safeDefaults = validated.Where(static theme => theme.IsSafeDefault).ToList();
        if (safeDefaults.Count != 1)
            throw new InvalidOperationException("Exactly one installed theme must be the safe default.");

        _manifests = Array.AsReadOnly(validated
            .OrderBy(static theme => theme.Id, StringComparer.Ordinal)
            .ThenBy(static theme => theme.Version, StringComparer.Ordinal)
            .ToArray());
        _byIdentity = _manifests.ToDictionary(static theme => (theme.Id, theme.Version));
        SafeDefault = safeDefaults[0];
    }

    /// <inheritdoc />
    public InstalledThemeManifest SafeDefault { get; }

    /// <inheritdoc />
    public IReadOnlyList<InstalledThemeManifest> GetAll() => _manifests;

    /// <inheritdoc />
    public InstalledThemeManifest? Find(string themeId, string themeVersion)
        => _byIdentity.GetValueOrDefault((themeId, themeVersion));

    private static InstalledThemeManifest Validate(InstalledThemeManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!IsIdentifier(manifest.Id))
            throw new InvalidOperationException($"Theme id '{manifest.Id}' is not a lowercase deployment identifier.");
        if (!IsVersion(manifest.Version))
            throw new InvalidOperationException($"Theme version '{manifest.Version}' is not a valid exact version label.");
        if (string.IsNullOrWhiteSpace(manifest.Name) || string.IsNullOrWhiteSpace(manifest.Author))
            throw new InvalidOperationException($"Theme '{manifest.Id}@{manifest.Version}' must have a name and author.");
        if (manifest.Stylesheets is null || manifest.Stylesheets.Count == 0)
            throw new InvalidOperationException($"Theme '{manifest.Id}@{manifest.Version}' has no compiled stylesheet assets.");

        var expectedPrefix = $"/_content/Aero.Cms.Modules.Theming/themes/{manifest.Id}/{manifest.Version}/";
        var assets = Array.AsReadOnly(manifest.Stylesheets
            .Select(asset => ValidateAsset(manifest, asset, expectedPrefix))
            .OrderBy(static asset => asset.Order)
            .ToArray());
        if (assets.Select(static asset => asset.Path).Distinct(StringComparer.Ordinal).Count() != assets.Count)
            throw new InvalidOperationException($"Theme '{manifest.Id}@{manifest.Version}' contains a duplicate stylesheet path.");
        if (assets.Select(static asset => asset.Order).Distinct().Count() != assets.Count)
            throw new InvalidOperationException($"Theme '{manifest.Id}@{manifest.Version}' contains duplicate stylesheet ordering.");
        if (manifest.ThumbnailUrl is not null && !IsLocalAssetPath(manifest.ThumbnailUrl))
            throw new InvalidOperationException($"Theme '{manifest.Id}@{manifest.Version}' has a non-local thumbnail path.");

        return manifest with { Stylesheets = assets };
    }

    private static ThemeStylesheetAsset ValidateAsset(InstalledThemeManifest manifest, ThemeStylesheetAsset asset, string expectedPrefix)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (!IsLocalAssetPath(asset.Path) ||
            !asset.Path.StartsWith(expectedPrefix, StringComparison.Ordinal) ||
            !asset.Path.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Theme '{manifest.Id}@{manifest.Version}' contains an invalid compiled stylesheet path.");
        return asset;
    }

    private static bool IsLocalAssetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal) ||
            path.StartsWith("//", StringComparison.Ordinal) || path.Contains('\\') ||
            path.Contains('?') || path.Contains('#') || path.Contains('%') ||
            path.Contains("://", StringComparison.OrdinalIgnoreCase) || path.Any(char.IsControl))
            return false;

        return !path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(static segment => segment is "." or "..");
    }

    private static bool IsIdentifier(string value)
        => !string.IsNullOrWhiteSpace(value) && value.All(static character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

    private static bool IsVersion(string value)
        => !string.IsNullOrWhiteSpace(value) && value.All(static character =>
            character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-');
}

internal static class BuiltInThemeManifest
{
    public static InstalledThemeManifest Create() => new(
        BuiltInThemeDefaults.Id,
        BuiltInThemeDefaults.Version,
        "Aero Safe",
        "Aero CMS",
        "Minimal local fallback styles for sites whose selected theme is unavailable.",
        ThemeAuthoringEngine.Scss,
        [
            new ThemeStylesheetAsset("/_content/Aero.Cms.Modules.Theming/themes/aero-safe/1.0.0/framework.css", 0),
            new ThemeStylesheetAsset("/_content/Aero.Cms.Modules.Theming/themes/aero-safe/1.0.0/theme.css", 100)
        ],
        IsSafeDefault: true);
}

/// <summary>Resolves the exact site selection attached by site-resolution middleware.</summary>
public sealed class SiteThemeStylesheetResolver(
    IHttpContextAccessor httpContextAccessor,
    IThemeCatalog catalog,
    IThemeLibrary library,
    ILogger<SiteThemeStylesheetResolver> logger) : IThemeStylesheetResolver
{
    /// <inheritdoc />
    public async ValueTask<ResolvedThemeStylesheets> ResolveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var slice = httpContextAccessor.HttpContext?.Features.Get<IAeroSiteSlice>();
        if (slice is null)
        {
            logger.LogDebug("No public site context is available; using the safe built-in theme.");
            return ToResolved(catalog.SafeDefault, 0, true);
        }

        var selected = await library.ResolveAsync(slice.TenantId, slice.ThemeId, slice.ThemeVersion, cancellationToken);
        if (selected is not null)
            return new(selected.ThemeId, selected.ThemeVersion, selected.DataThemeName, slice.ThemeRevision, selected.Stylesheets, false);

        logger.LogWarning(
            "Site {SiteId} selects missing theme {ThemeId}@{ThemeVersion}; rendering safe default without changing the stored selection.",
            slice.SiteId, slice.ThemeId, slice.ThemeVersion);
        return ToResolved(catalog.SafeDefault, slice.ThemeRevision, true);
    }

    private static ResolvedThemeStylesheets ToResolved(InstalledThemeManifest manifest, long revision, bool usedSafeDefault)
        => new(manifest.Id, manifest.Version, BuiltInThemeDefaults.ComponentThemeName, revision, manifest.Stylesheets, usedSafeDefault);
}
