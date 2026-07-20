using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Stores culture-specific display text for a tag identifier.
/// </summary>
public sealed class TagTranslation : SableDocument, IAuditable, ICultureAware
{
        /// <summary>
    /// Gets or sets the tag document identifier this translation describes; the reference is not enforced by this type.
    /// </summary>
public long TagId { get; set; }
        /// <summary>
    /// Gets or sets the culture key, defaulting to the CMS default culture without normalization.
    /// </summary>
public string Culture { get; set; } = SitesModel.DefaultCultureName;
        /// <summary>
    /// Gets or sets the required-initialized localized tag label; callers remain responsible for validation.
    /// </summary>
public string Name { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets optional localized descriptive text.
    /// </summary>
    public string? Description { get; set; }

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
