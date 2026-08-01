namespace Aero.Cms.Core;

/// <summary>
/// Defines the standard AeroCMS role-name identifiers.
/// </summary>
/// <remarks>
/// These constants identify roles only. Possessing a name does not itself enforce permissions;
/// authorization policy and permission services determine effective access.
/// </remarks>
public static class CmsRoleNames
{
    /// <summary>The administrator role name.</summary>
    public const string Admin = nameof(Admin);
    /// <summary>The editor role name.</summary>
    public const string Editor = nameof(Editor);
    /// <summary>The contributor role name.</summary>
    public const string Contributor = nameof(Contributor);
    /// <summary>The view-only role name.</summary>
    public const string ViewOnly = nameof(ViewOnly);

    /// <summary>Gets the standard role names as a comma-separated string.</summary>
public const string ManagerRoleCsv = Admin + "," + Editor + "," + Contributor + "," + ViewOnly;

    /// <summary>Gets all standard role-name identifiers in declaration order.</summary>
public static IReadOnlyList<string> All { get; } =
    [
        Admin,
        Editor,
        Contributor,
        ViewOnly
    ];
}

