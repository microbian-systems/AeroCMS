using Ganss.Xss;

namespace Aero.Cms.Html;

/// <summary>Creates the canonical allowlist used for CMS rich-text field HTML.</summary>
public static class RichTextHtmlSanitizer
{
    private static readonly string[] Tags =
    [
        "a", "b", "blockquote", "br", "div", "em", "h1", "h2", "h3", "h4", "h5", "h6",
        "i", "li", "ol", "p", "s", "span", "strike", "strong", "sub", "sup", "u", "ul"
    ];

    /// <summary>Creates an isolated sanitizer because sanitizer instances are mutable.</summary>
    public static IHtmlSanitizer Create()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(Tags);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(["href", "title"]);
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedAtRules.Clear();
        sanitizer.AllowedClasses.Clear();
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(["http", "https", "mailto", "tel"]);
        sanitizer.AllowDataAttributes = false;
        return sanitizer;
    }
}
