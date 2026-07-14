namespace Aero.Cms.Html;

/// <summary>
/// Describes a node move relative to another stable node identity.
/// Browser adapters use this semantic contract instead of model collection indexes.
/// </summary>
public enum HtmlRelativePlacement
{
    Before,
    After,
    Inside
}
