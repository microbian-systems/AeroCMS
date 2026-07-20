using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Stores tenant display and hostname metadata without enforcing hostname normalization or uniqueness.
/// </summary>
public class TenantModel : SableDocument, IAuditable
{
        /// <summary>
    /// Gets or sets the caller-supplied tenant display name; this type does not validate it.
    /// </summary>
public string Name { get; set; } = default!;
        /// <summary>
    /// Gets or sets the caller-supplied hostname used as tenant metadata; no normalization or uniqueness is enforced here.
    /// </summary>
public string Hostname { get; set; } = default!;
        /// <summary>
    /// Gets or sets optional free-form tenant notes.
    /// </summary>
    public string? Notes { get; set; } = null;

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
