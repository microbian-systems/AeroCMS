namespace Aero.Cms.Html;

/// <summary>
/// Describes a node move relative to another stable node identity.
/// Browser adapters use this semantic contract instead of model collection indexes.
/// </summary>
public enum HtmlRelativePlacement
{
    /// <summary>Places the source immediately before the target in the target's parent.</summary>
    Before,
    /// <summary>Places the source immediately after the target in the target's parent.</summary>
    After,
    /// <summary>Places the source at the end of the target's children.</summary>
    Inside
}
