namespace Aero.Cms.Core;

/// <summary>
/// default Aero CMS roles
/// </summary>
public static class AeroCmsRoles
{
    /// <summary>
    /// aero application administrator
    /// </summary>
    public const string Admin = nameof(Admin);
    /// <summary>
    /// content editor for aero cms
    /// <remarks>CRUD perms for content and can approve others content</remarks>
    /// </summary>
    public const string Editor = nameof(Editor);
    /// <summary>
    /// blog authors who can create, edit and delete their own content
    /// <remarks>contributor's cannot edit other users content (they can view it however</remarks>
    /// </summary>
    public const string Contributor = nameof(Contributor);
    /// <summary>
    /// read-only access to the admin area of the aero cms
    /// <remarks>cannot edit/insert/update content of any kind</remarks>
    /// </summary>
    public const string ViewOnly = nameof(ViewOnly);

    public static IReadOnlyList<string> All { get; } =
    [
        Admin,
        Editor,
        Contributor,
        ViewOnly
    ];
}

