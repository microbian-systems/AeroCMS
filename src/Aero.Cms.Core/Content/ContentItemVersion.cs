using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content;

/// <summary>
/// Stores one persisted field-value snapshot for a content item.
/// </summary>
public sealed class ContentItemVersion : SableDocument, IAuditable
{
    /// <summary>Gets or sets the identifier of the versioned content item.</summary>
    public long ContentItemId { get; set; }
    /// <summary>Gets or sets the item's version sequence number.</summary>
    public int VersionNumber { get; set; }
    /// <summary>Gets or sets the serialized JSON field values captured by this version.</summary>
    public string FieldsJson { get; set; } = "{}";
    /// <summary>Gets or sets the UTC timestamp at which this version was created.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    // IAuditable
    /// <summary>Gets or sets the audit creation timestamp.</summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Gets or sets the most recent audit modification timestamp.</summary>
    public DateTimeOffset? ModifiedOn { get; set; }
    /// <summary>Gets or sets the identity that created this version, if recorded.</summary>
    public string? CreatedBy { get; set; }
    /// <summary>Gets or sets the identity that last modified this version, if recorded.</summary>
    public string? ModifiedBy { get; set; }
}
