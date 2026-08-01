namespace Aero.Cms.Html;

/// <summary>
/// The supported child-content model for an HTML element definition.
/// </summary>
public enum HtmlChildModel
{
    /// <summary>The element cannot contain child nodes.</summary>
    None,
    /// <summary>The element accepts supported flow-content nodes.</summary>
    Flow,
    /// <summary>The element accepts supported phrasing-content nodes.</summary>
    Phrasing,
    /// <summary>The element accepts list-item elements.</summary>
    List,
    /// <summary>The element accepts element nodes but not literal text nodes.</summary>
    Elements,
    /// <summary>The element accepts literal text nodes only.</summary>
    Text
}
