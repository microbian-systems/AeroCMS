namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Represents a record for SiteViewModel.
/// </summary>
[Alias("SiteViewModel")]
[GenerateSerializer]
public record SiteViewModel : AeroEntityViewModel
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
[Id(1)]
    public string? Name { get; set; }
        /// <summary>
    /// Gets or sets the Primary Host.
    /// </summary>
[Id(2)]
    public string? PrimaryHost { get; set; }
        /// <summary>
    /// Gets or sets the Hosts.
    /// </summary>
[Id(3)]
    public List<string> Hosts { get; set; } = [];
        /// <summary>
    /// Gets or sets the Is Enabled.
    /// </summary>
[Id(4)]
    public bool IsEnabled { get; set; } = true;
        /// <summary>
    /// Gets or sets the Default Culture.
    /// </summary>
[Id(5)]
    public string? DefaultCulture { get; set; }
        /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
[Id(6)]
    public long TenantId { get; set; }
        /// <summary>
    /// Gets or sets the Supported Cultures.
    /// </summary>
[Id(7)]
    public List<string> SupportedCultures { get; set; } = ["en-US"];
        /// <summary>
    /// Gets or sets the site's framework-neutral style profile.
    /// </summary>
[Id(8)]
    public SiteStyleProfileViewModel StyleProfile { get; set; } = new();
}

/// <summary>
/// Represents a record for SiteErrorViewModel.
/// </summary>
[GenerateSerializer]
[Alias("SiteErrorViewModel")]
public record SiteErrorViewModel : AeroErrorViewModel<SiteViewModel>;
