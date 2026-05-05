using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Maps a user to a site with per-site permissions.
/// Each record grants a user a set of permissions on a specific site.
/// Admins bypass this check entirely (they have access to all sites).
/// </summary>
public class UserSiteAssignment : Entity
{
    /// <summary>The user's ID (ASP.NET Identity user ID, stored as long).</summary>
    public long UserId { get; set; }

    /// <summary>The site this assignment grants access to.</summary>
    public long SiteId { get; set; }

    /// <summary>
    /// Permissions granted on this site.
    /// Standard values: "create", "read", "update", "delete".
    /// Custom permission strings may be added by modules.
    /// </summary>
    public List<string> Permissions { get; set; } = [];
}
