namespace Aero.Cms.Contracts.Models;

/// <summary>
/// Framework-neutral site information transferred to manager clients without Orleans
/// serialization dependencies.
/// </summary>
/// <param name="Id">The site's identifier.</param>
/// <param name="Name">The site's display name, if available.</param>
/// <param name="PrimaryHost">The site's primary host name, if configured.</param>
/// <param name="IsEnabled">Whether the site is enabled.</param>
/// <param name="DefaultCulture">The site's default culture, if configured.</param>
/// <param name="TenantId">The tenant that owns the site.</param>
/// <param name="SupportedCultures">The cultures supported by the site, if provided.</param>
public record SiteInfo(
    long Id,
    string? Name,
    string? PrimaryHost,
    bool IsEnabled,
    string? DefaultCulture,
    long TenantId,
    IReadOnlyList<string>? SupportedCultures = null);
