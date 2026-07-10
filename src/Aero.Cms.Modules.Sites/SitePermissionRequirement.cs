using Microsoft.AspNetCore.Authorization;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Authorization requirement for a specific site-level permission.
/// Use with ASP.NET Core policy-based authorization: <c>[Authorize(Policy = "site:read")]</c>.
/// </summary>
public sealed class SitePermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// The permission value to check (e.g. "create", "read", "update", "delete").
    /// </summary>
    public string Permission { get; }

        /// <summary>
    /// Initializes a new instance of the <see cref="SitePermissionRequirement"/> class.
    /// </summary>
public SitePermissionRequirement(string permission)
    {
        Permission = permission;
    }
}
