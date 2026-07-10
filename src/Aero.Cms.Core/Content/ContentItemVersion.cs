using Aero.Core.Entities;

namespace Aero.Cms.Core.Content;

/// <summary>
/// Represents a class for ContentItemVersion.
/// </summary>
public sealed class ContentItemVersion : Entity
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
}
