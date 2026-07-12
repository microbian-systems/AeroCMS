using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content;

/// <summary>
/// Represents a class for ContentItemVersion.
/// </summary>
public sealed class ContentItemVersion : SableDocument, IAuditable
{
        /// <summary>
    /// Gets or sets the Content Item Id.
    /// </summary>
public long ContentItemId { get; set; }
        /// <summary>
    /// Gets or sets the Version Number.
    /// </summary>
public int VersionNumber { get; set; }
        /// <summary>
    /// Gets or sets the Fields Json.
    /// </summary>
public string FieldsJson { get; set; } = "{}";
        /// <summary>
    /// Gets or sets the Created Utc.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    // IAuditable
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
