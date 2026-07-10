namespace Aero.Cms.Abstractions.Models;


/// <summary>
/// Represents a record for TenantViewModel.
/// </summary>
[Alias("TenantViewModel")]
[GenerateSerializer]
public record TenantViewModel : AeroEntityViewModel
{
        /// <summary>
    /// Gets or sets the Account Id.
    /// </summary>
[Id(0)]
    public long AccountId { get; set; }
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
[Id(1)]
    public string? Name { get; set; }
        /// <summary>
    /// Gets or sets the Host.
    /// </summary>
[Id(2)]
    public string? Host { get; set; }
        /// <summary>
    /// Gets or sets the Settings.
    /// </summary>
[Id(3)]
    public List<(long siteId, string siteName)> Settings { get; } = [];
}


/// <summary>
/// Represents a record for TenantErrorViewModel.
/// </summary>
[GenerateSerializer]
[Alias("TenantErrorViewModel")]
public record TenantErrorViewModel : AeroErrorViewModel<TenantViewModel>;