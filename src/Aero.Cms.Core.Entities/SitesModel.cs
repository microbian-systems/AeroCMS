using Aero.Core.Data;
using Aero.Cms.Html;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents a hosted site within Aero CMS.
/// Multi-domain/CNAME support is managed via the separate <see cref="SiteHost"/> entity.
/// </summary>
public class SitesModel : SableDocument, IAuditable
{
        /// <summary>
    /// The culture label used to initialize culture-related properties.
    /// </summary>
public const string DefaultCultureName = "en-US";

        /// <summary>
    /// Gets or sets the tenant identifier recorded for this site; the relationship is not enforced by this entity.
    /// </summary>
public long TenantId { get; set; }
        /// <summary>
    /// Gets or sets the optional caller-supplied site display name.
    /// </summary>
public string? Name { get; set; }
        /// <summary>
    /// Gets or sets optional descriptive text stored with the site.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the stored enabled flag; enforcement is the responsibility of consuming code.
    /// </summary>
public bool IsEnabled { get; set; }
        /// <summary>
    /// Gets or sets the optional default culture label, initialized to <see cref="DefaultCultureName"/> without normalization.
    /// </summary>
public string? DefaultCulture { get; set; } = DefaultCultureName;
        /// <summary>
    /// Gets or sets the mutable collection of culture labels, initialized with <see cref="DefaultCultureName"/>; contents are not validated here.
    /// </summary>
    public List<string> SupportedCultures { get; set; } = [DefaultCultureName];

    /// <summary>
    /// Gets or sets the framework-neutral site style profile.
    /// </summary>
    public StyleProfileSettings StyleProfile { get; set; } = new();

    // IAuditable
    /// <summary>Gets or sets the creation timestamp. The default is UTC, but setters do not enforce an offset.</summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Gets or sets the last-modified timestamp; callers and persistence conventionally use UTC, but setters do not enforce it.</summary>
    public DateTimeOffset? ModifiedOn { get; set; }
    /// <summary>Gets or sets the actor recorded as creating this document, when available.</summary>
    public string? CreatedBy { get; set; }
    /// <summary>Gets or sets the actor recorded as last modifying this document, when available.</summary>
    public string? ModifiedBy { get; set; }
}


