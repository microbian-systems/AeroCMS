using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents a single host/domain assigned to a site.
/// Enables unique-indexed multi-domain support at the database level.
/// </summary>
public class SiteHost : SableDocument, IAuditable
{
    /// <summary>The site that owns this host.</summary>
    public long SiteId { get; set; }

    /// <summary>
    /// The normalized host/domain string (e.g. "example.com").
    /// Must be globally unique — enforced by an AeroDB unique index.
    /// </summary>
    public string Host { get; set; } = null!;

    /// <summary>
    /// Whether this host is the canonical/primary domain for the site.
    /// Exactly one <see cref="SiteHost"/> per site should be primary.
    /// </summary>
    public bool IsPrimary { get; set; }

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
