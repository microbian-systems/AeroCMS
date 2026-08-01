namespace Aero.Cms.Abstractions.Theming;

/// <summary>
/// Identifies the source format used by a trusted theme author before deployment.
/// </summary>
/// <remarks>The runtime serves only the compiled browser assets declared by the manifest.</remarks>
public enum ThemeAuthoringEngine
{
    /// <summary>The theme was authored as browser-ready CSS.</summary>
    Css,

    /// <summary>The theme was authored as SCSS and compiled before deployment.</summary>
    Scss,

    /// <summary>The theme was authored with Tailwind CSS and compiled before deployment.</summary>
    TailwindCss
}

/// <summary>
/// Describes one local, browser-ready stylesheet exposed through Static Web Assets.
/// </summary>
/// <param name="Path">The application-relative asset path.</param>
/// <param name="Order">The order in which the stylesheet is linked.</param>
public sealed record ThemeStylesheetAsset(string Path, int Order);

/// <summary>
/// Describes one exact version of a deployment-installed theme.
/// </summary>
public sealed record InstalledThemeManifest(
    string Id,
    string Version,
    string Name,
    string Author,
    string Description,
    ThemeAuthoringEngine AuthoringEngine,
    IReadOnlyList<ThemeStylesheetAsset> Stylesheets,
    string? ThumbnailUrl = null,
    bool IsSafeDefault = false);

/// <summary>
/// Provides the immutable catalog of themes installed with the application deployment.
/// </summary>
public interface IThemeCatalog
{
    /// <summary>Gets every installed exact theme version in deterministic order.</summary>
    IReadOnlyList<InstalledThemeManifest> GetAll();

    /// <summary>Finds an exact theme identifier and version.</summary>
    InstalledThemeManifest? Find(string themeId, string themeVersion);

    /// <summary>Gets the built-in theme used when a persisted selection cannot be resolved.</summary>
    InstalledThemeManifest SafeDefault { get; }
}

/// <summary>
/// Contains the local stylesheet assets selected for the current request.
/// </summary>
public sealed record ResolvedThemeStylesheets(
    string ThemeId,
    string ThemeVersion,
    long ThemeRevision,
    IReadOnlyList<ThemeStylesheetAsset> Stylesheets,
    bool UsedSafeDefault);

/// <summary>
/// Resolves the exact current-site theme to browser-ready local stylesheet assets.
/// </summary>
public interface IThemeStylesheetResolver
{
    /// <summary>Resolves the current request without mutating persisted site selection.</summary>
    ValueTask<ResolvedThemeStylesheets> ResolveAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the exact built-in selection assigned to newly created sites.
/// </summary>
public static class BuiltInThemeDefaults
{
    /// <summary>The DaisyUI component-token theme applied at site rendering boundaries.</summary>
    public const string ComponentThemeName = "corporate";

    /// <summary>The stable identifier of the safe built-in theme.</summary>
    public const string Id = "aero-safe";

    /// <summary>The exact deployed version of the safe built-in theme.</summary>
    public const string Version = "1.0.0";
}
