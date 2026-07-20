using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Stores culture-specific display text for a product identifier.
/// </summary>
public sealed class ProductTranslation : SableDocument, IAuditable, ICultureAware
{
        /// <summary>
    /// Gets or sets the product document identifier this translation describes; the reference is not enforced by this type.
    /// </summary>
public long ProductId { get; set; }
        /// <summary>
    /// Gets or sets the culture key, defaulting to the CMS default culture without normalization.
    /// </summary>
public string Culture { get; set; } = SitesModel.DefaultCultureName;
        /// <summary>
    /// Gets or sets the required-initialized localized product name; callers remain responsible for validation.
    /// </summary>
public string Name { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets optional localized long-form description text.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets optional localized short description text.
    /// </summary>
    public string? ShortDescription { get; set; }

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
