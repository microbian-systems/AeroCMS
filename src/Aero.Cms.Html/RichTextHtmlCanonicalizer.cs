using System.Globalization;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Aero.Cms.Html;

/// <summary>
/// Converts the small set of inline styles emitted by browser rich-text commands into
/// semantic HTML before the CMS security allowlist removes arbitrary styling.
/// </summary>
public static class RichTextHtmlCanonicalizer
{
    /// <summary>Canonicalizes editor-generated emphasis while discarding all inline CSS.</summary>
    public static string CanonicalizeEditorFormatting(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var parser = new HtmlParser();
        var document = parser.ParseDocument("<html><body></body></html>");
        var body = document.Body ?? throw new InvalidOperationException("An HTML body could not be created.");

        foreach (var node in parser.ParseFragment(html, body).ToArray())
        {
            body.AppendChild(node);
        }

        foreach (var element in body.QuerySelectorAll("[style]").ToArray())
        {
            var formatting = ParseFormatting(element.GetAttribute("style"));
            element.RemoveAttribute("style");

            foreach (var tagName in formatting)
            {
                var wrapper = document.CreateElement(tagName);
                while (element.FirstChild is { } child)
                {
                    wrapper.AppendChild(child);
                }

                element.AppendChild(wrapper);
            }
        }

        return body.InnerHtml;
    }

    private static IReadOnlyList<string> ParseFormatting(string? style)
    {
        var tags = new List<string>(capacity: 4);
        foreach (var declaration in (style ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = declaration.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var property = declaration[..separator].Trim();
            var value = declaration[(separator + 1)..].Trim();
            if (property.Equals("font-weight", StringComparison.OrdinalIgnoreCase) && IsBold(value))
            {
                AddOnce(tags, "strong");
            }
            else if (property.Equals("font-style", StringComparison.OrdinalIgnoreCase) &&
                     (value.Equals("italic", StringComparison.OrdinalIgnoreCase) ||
                      value.Equals("oblique", StringComparison.OrdinalIgnoreCase)))
            {
                AddOnce(tags, "em");
            }
            else if (property.Equals("text-decoration", StringComparison.OrdinalIgnoreCase) ||
                     property.Equals("text-decoration-line", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Contains("underline", StringComparison.OrdinalIgnoreCase))
                {
                    AddOnce(tags, "u");
                }

                if (value.Contains("line-through", StringComparison.OrdinalIgnoreCase))
                {
                    AddOnce(tags, "s");
                }
            }
        }

        return tags;
    }

    private static bool IsBold(string value)
    {
        if (value.Equals("bold", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("bolder", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var weight) && weight >= 600;
    }

    private static void AddOnce(List<string> tags, string tagName)
    {
        if (!tags.Contains(tagName, StringComparer.Ordinal))
        {
            tags.Add(tagName);
        }
    }
}
