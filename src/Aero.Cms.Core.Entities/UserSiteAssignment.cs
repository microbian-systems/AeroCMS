using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Maps a user to a site with per-site permissions.
/// Each record grants a user a set of permissions on a specific site.
/// Admins bypass this check entirely (they have access to all sites).
/// </summary>
public class UserSiteAssignment : SableDocument, IAuditable // todo - rename UserSiteAssignment -> UserSitePerms
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
