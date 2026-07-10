using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents a hosted site within Aero CMS.
/// Multi-domain/CNAME support is managed via the separate <see cref="SiteHost"/> entity.
/// </summary>
public class SitesModel : Entity
{
        /// <summary>
    /// DefaultCultureName.
    /// </summary>
public const string DefaultCultureName = "en-US";

        /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
public long TenantId { get; set; }
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string? Name { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Is Enabled.
    /// </summary>
public bool IsEnabled { get; set; }
        /// <summary>
    /// Gets or sets the Default Culture.
    /// </summary>
public string? DefaultCulture { get; set; } = DefaultCultureName;
        /// <summary>
    /// Gets or sets the Supported Cultures.
    /// </summary>
public List<string> SupportedCultures { get; set; } = [DefaultCultureName];
}


