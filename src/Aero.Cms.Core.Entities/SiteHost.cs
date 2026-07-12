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
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
