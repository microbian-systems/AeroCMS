using Aero.Core;

namespace Aero.Cms.Html;

/// <summary>
/// A single node in the ordered, rooted HTML page tree.
/// </summary>
public sealed class HtmlNode
{
    /// <summary>
    /// Stable editor identity. This is never emitted as an HTML attribute by default.
    /// </summary>
    public long NodeId { get; set; } = Snowflake.NewId();

    /// <summary>
    /// Gets or sets whether this node is a fragment, element, or text node.
    /// </summary>
    public HtmlNodeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the lower-case HTML tag for element nodes.
    /// </summary>
    public string? TagName { get; set; }

    /// <summary>
    /// Gets or sets literal content for text nodes.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets element attributes. Catalog validation determines which entries are permitted.
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = [];

    /// <summary>
    /// Gets or sets optional framework/profile-specific classes selected in advanced editing.
    /// </summary>
    public List<string> ThemeClasses { get; set; } = [];

    /// <summary>
    /// Gets or sets constrained, framework-neutral style intent for this element.
    /// </summary>
    public HtmlStyle? Style { get; set; }

    /// <summary>
    /// Gets or sets ordered child nodes. Text and void-element validation requires this to be empty.
    /// </summary>
    public List<HtmlNode> Children { get; set; } = [];

    /// <summary>
    /// Creates the root fragment for a page-content document.
    /// </summary>
    /// <returns>A fresh fragment with a generated stable identity.</returns>
    public static HtmlNode CreateFragment() => new() { Kind = HtmlNodeKind.Fragment };

    /// <summary>
    /// Creates an element node. Catalog validation occurs at the application boundary.
    /// </summary>
    /// <param name="tagName">The tag stored on the node; this factory does not validate or normalize it.</param>
    /// <returns>A fresh element with a generated stable identity.</returns>
    public static HtmlNode CreateElement(string tagName) => new()
    {
        Kind = HtmlNodeKind.Element,
        TagName = tagName
    };

    /// <summary>
    /// Creates a literal text node.
    /// </summary>
    /// <param name="text">The unencoded literal text. Encoding occurs during static rendering.</param>
    /// <returns>A fresh text node with a generated stable identity.</returns>
    public static HtmlNode CreateText(string text) => new()
    {
        Kind = HtmlNodeKind.Text,
        Text = text
    };
}
