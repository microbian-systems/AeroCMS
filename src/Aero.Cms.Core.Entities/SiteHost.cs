using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents a single host/domain assigned to a site.
/// Enables unique-indexed multi-domain support at the database level.
/// </summary>
public class SiteHost : Entity
{
    /// <summary>The site that owns this host.</summary>
    public long SiteId { get; set; }

    /// <summary>
    /// The normalized host/domain string (e.g. "example.com").
    /// Must be globally unique — enforced by a Marten unique index.
    /// </summary>
    public string Host { get; set; } = null!;

    /// <summary>
    /// Whether this host is the canonical/primary domain for the site.
    /// Exactly one <see cref="SiteHost"/> per site should be primary.
    /// </summary>
    public bool IsPrimary { get; set; }
}
