using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Stores a mutable tag name and optional description.
/// </summary>
public class TagModel : SableDocument, IAuditable
{
        /// <summary>
    /// Gets or sets the optional caller-supplied tag label; this document does not enforce uniqueness.
    /// </summary>
public string? Name { get; set; }
        /// <summary>
    /// Gets or sets optional descriptive text stored with the tag.
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
