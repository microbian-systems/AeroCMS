using Microsoft.AspNetCore.Authorization;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Requires a named permission on the site selected by the manager cookie.
/// </summary>
/// <remarks>
/// <see cref="SitePermissionHandler"/> interprets the value case-insensitively and grants
/// administrators a bypass. The requirement itself does not validate or normalize the string.
/// </remarks>
public sealed class SitePermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Gets the permission value checked against a user-site assignment.
    /// </summary>
    public string Permission { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SitePermissionRequirement"/> class.
    /// </summary>
    /// <param name="permission">The permission name to compare case-insensitively.</param>
public SitePermissionRequirement(string permission)
    {
        Permission = permission;
    }
}
