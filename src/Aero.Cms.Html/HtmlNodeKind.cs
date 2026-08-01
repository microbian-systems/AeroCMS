namespace Aero.Cms.Html;

/// <summary>
/// Describes the role of a node in a persisted HTML page tree.
/// </summary>
public enum HtmlNodeKind
{
    /// <summary>A non-rendered container for ordered top-level nodes.</summary>
    Fragment,
    /// <summary>A catalog-backed HTML element.</summary>
    Element,
    /// <summary>Literal text that is encoded during static rendering.</summary>
    Text
}
