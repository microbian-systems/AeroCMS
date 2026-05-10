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

    public SitePermissionRequirement(string permission)
    {
        Permission = permission;
    }
}
