namespace Aero.Cms.Abstractions.Interfaces;

/// <summary>
/// Attached to the <c>HttpContext.Features</c> collection by the site resolution
/// middleware to communicate the current site to downstream middleware and services.
/// </summary>
public interface IAeroSiteSlice
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
long SiteId { get; }
        /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
long TenantId { get; }
        /// <summary>
    /// Gets or sets the Default Culture.
    /// </summary>
string? DefaultCulture { get; }
        /// <summary>
    /// Gets or sets the Supported Cultures.
    /// </summary>
IReadOnlyList<string> SupportedCultures { get; }
    /// <summary>Gets the exact selected deployment-installed theme identifier.</summary>
string ThemeId { get; }
    /// <summary>Gets the exact selected deployment-installed theme version.</summary>
string ThemeVersion { get; }
    /// <summary>Gets the persisted theme-selection revision.</summary>
long ThemeRevision { get; }
}

/// <summary>
/// Default implementation of <see cref="IAeroSiteSlice"/>.
/// </summary>
public sealed class AeroSiteSlice : IAeroSiteSlice
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; init; }
        /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
public long TenantId { get; init; }
        /// <summary>
    /// Gets or sets the Default Culture.
    /// </summary>
public string? DefaultCulture { get; init; }
        /// <summary>
    /// Gets or sets the Supported Cultures.
    /// </summary>
public IReadOnlyList<string> SupportedCultures { get; init; } = [];
    /// <inheritdoc />
public string ThemeId { get; init; } = Theming.BuiltInThemeDefaults.Id;
    /// <inheritdoc />
public string ThemeVersion { get; init; } = Theming.BuiltInThemeDefaults.Version;
    /// <inheritdoc />
public long ThemeRevision { get; init; } = 1;
}
