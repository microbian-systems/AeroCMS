using System.Globalization;
using System.Text;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Lossless, fail-closed visitor for the Markdown-representable subset of an HTML tree.
/// </summary>
/// <param name="contentValidator">The authoritative validator applied before visiting the tree.</param>
/// <param name="limits">Resource limits for generated Markdown.</param>
internal sealed class MarkdownTreeExporter(
    IHtmlContentValidator contentValidator,
    MarkdownInterchangeLimits limits)
{
    /// <summary>
    /// Validates and renders the losslessly representable subset without returning partial Markdown.
    /// </summary>
    public Result<string> Export(HtmlPageContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var validation = contentValidator.Validate(content);
        if (validation is Result<bool>.Failure failure)
        {
            return failure.Error;
        }

        try
        {
            var markdown = RenderBlocks(content.Root.Children);
            if (markdown.Length > limits.MaximumExportLength)
            {
                return AeroError.ValidationError(
                    [$"Markdown export exceeds the maximum length of {limits.MaximumExportLength} characters."]);
            }

            return new Result<string>.Ok(
                markdown.Length == 0 ? string.Empty : markdown + Environment.NewLine);
        }
        catch (MarkdownExportException exception)
        {
            return AeroError.ValidationError([exception.Message]);
        }
    }

    /// <summary>Renders sibling block nodes separated by canonical blank lines.</summary>
    private static string RenderBlocks(IEnumerable<HtmlNode> nodes)
    {
        var blocks = nodes.Select(node =>
        {
            var rendered = RenderBlock(node);
            if (string.IsNullOrWhiteSpace(rendered))
            {
                throw new MarkdownExportException(
                    $"Empty or whitespace-only <{node.TagName}> content cannot be represented losslessly in Markdown.");
            }

            return rendered;
        });
        return string.Join("\n\n", blocks);
    }

    /// <summary>Dispatches one block element and rejects unsupported or presentation-bearing nodes.</summary>
    private static string RenderBlock(HtmlNode node)
    {
        EnsureElement(node);
        EnsureNoPresentation(node);

        return node.TagName switch
        {
            "h1" => RenderHeading(node, 1),
            "h2" => RenderHeading(node, 2),
            "h3" => RenderHeading(node, 3),
            "h4" => RenderHeading(node, 4),
            "h5" => RenderHeading(node, 5),
            "h6" => RenderHeading(node, 6),
            "p" => RenderParagraph(node),
            "blockquote" => RenderBlockquote(node),
            "ul" => RenderList(node, ordered: false),
            "ol" => RenderList(node, ordered: true),
            "hr" => RenderRule(node),
            "pre" => RenderCodeBlock(node),
            _ => throw UnsupportedElement(node)
        };
    }

    /// <summary>Renders an ATX heading after enforcing its attribute-free inline-only shape.</summary>
    private static string RenderHeading(HtmlNode node, int level)
    {
        EnsureAttributes(node);
        return $"{new string('#', level)} {RenderInlineChildren(node)}";
    }

    /// <summary>Renders a paragraph from losslessly representable inline children.</summary>
    private static string RenderParagraph(HtmlNode node)
    {
        EnsureAttributes(node);
        return RenderInlineChildren(node);
    }

    /// <summary>Prefixes every rendered child line so multi-line quotations preserve their block structure.</summary>
    private static string RenderBlockquote(HtmlNode node)
    {
        EnsureAttributes(node);
        var body = RenderBlocks(node.Children);
        return string.Join(
            "\n",
            body.Split('\n').Select(line => line.Length == 0 ? ">" : $"> {line}"));
    }

    /// <summary>Renders an attribute-free, childless thematic break.</summary>
    private static string RenderRule(HtmlNode node)
    {
        EnsureAttributes(node);
        if (node.Children.Count > 0)
        {
            throw new MarkdownExportException("<hr> cannot contain children.");
        }

        return "---";
    }

    /// <summary>Renders a homogeneous list while preserving nesting and canonical indentation.</summary>
    private static string RenderList(HtmlNode node, bool ordered)
    {
        EnsureAttributes(node, ordered ? ["start"] : []);

        long number = 1;
        if (ordered && TryGetAttribute(node, "start", out var startValue)
            && (!long.TryParse(startValue, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number)
                || number < 0))
        {
            throw new MarkdownExportException(
                "Markdown can export an ordered-list start value only when it is a non-negative integer.");
        }

        var lines = new List<string>();
        foreach (var item in node.Children)
        {
            if (item.Kind is not HtmlNodeKind.Element || item.TagName != "li")
            {
                throw new MarkdownExportException($"<{node.TagName}> can export only <li> children.");
            }

            EnsureNoPresentation(item);
            EnsureAttributes(item);

            var prefix = ordered
                ? $"{number.ToString(CultureInfo.InvariantCulture)}. "
                : "- ";
            var body = RenderListItem(item);
            var itemLines = body.Split('\n');
            lines.Add(prefix + itemLines[0]);
            var continuation = new string(' ', prefix.Length);
            lines.AddRange(itemLines.Skip(1).Select(line =>
                line.Length == 0 ? string.Empty : continuation + line));
            number++;
        }

        return string.Join("\n", lines);
    }

    /// <summary>Renders one list item whose mixed inline and nested-list content remains unambiguous.</summary>
    private static string RenderListItem(HtmlNode item)
    {
        var chunks = new List<string>();
        var inline = new List<HtmlNode>();

        void FlushInline()
        {
            if (inline.Count == 0)
            {
                return;
            }

            chunks.Add(RenderInlineNodes(inline));
            inline.Clear();
        }

        foreach (var child in item.Children)
        {
            if (IsInlineNode(child))
            {
                inline.Add(child);
                continue;
            }

            FlushInline();
            if (child.Kind is not HtmlNodeKind.Element)
            {
                throw new MarkdownExportException("<li> contains content that Markdown cannot represent.");
            }

            chunks.Add(child.TagName == "p"
                ? RenderParagraph(child)
                : RenderBlock(child));
        }

        FlushInline();
        return chunks.Count == 0 ? string.Empty : string.Join("\n\n", chunks);
    }

    /// <summary>Selects a fence longer than any source run and validates the optional language token.</summary>
    private static string RenderCodeBlock(HtmlNode node)
    {
        EnsureAttributes(node);
        if (node.Children.Count != 1
            || node.Children[0].Kind is not HtmlNodeKind.Element
            || node.Children[0].TagName != "code")
        {
            throw new MarkdownExportException("<pre> must contain exactly one <code> element for Markdown export.");
        }

        var code = node.Children[0];
        EnsureNoPresentation(code);
        EnsureAttributes(code, ["class"]);
        if (code.Children.Any(child => child.Kind is not HtmlNodeKind.Text))
        {
            throw new MarkdownExportException("A fenced code block can contain literal text only.");
        }

        var language = string.Empty;
        if (TryGetAttribute(code, "class", out var className))
        {
            const string prefix = "language-";
            if (className is null
                || !className.StartsWith(prefix, StringComparison.Ordinal)
                || !IsLanguageName(className[prefix.Length..]))
            {
                throw new MarkdownExportException(
                    "A code-block class must contain one safe language-* identifier.");
            }

            language = className[prefix.Length..];
        }

        var text = string.Concat(code.Children.Select(child => child.Text));
        var fence = new string('`', Math.Max(3, LongestRun(text, '`') + 1));
        return $"{fence}{language}\n{text.TrimEnd('\r', '\n')}\n{fence}";
    }

    /// <summary>Renders all children through the inline-only visitor.</summary>
    private static string RenderInlineChildren(HtmlNode node) => RenderInlineNodes(node.Children);

    /// <summary>Concatenates inline nodes without adding layout whitespace.</summary>
    private static string RenderInlineNodes(IEnumerable<HtmlNode> nodes) =>
        string.Concat(nodes.Select(RenderInline));

    /// <summary>Dispatches one text or phrasing node and rejects block-only or unsupported elements.</summary>
    private static string RenderInline(HtmlNode node)
    {
        if (node.Kind is HtmlNodeKind.Text)
        {
            return EscapeText(node.Text ?? string.Empty);
        }

        EnsureElement(node);
        EnsureNoPresentation(node);

        return node.TagName switch
        {
            "strong" => WrapInline(node, "**", "**"),
            "em" => WrapInline(node, "*", "*"),
            "del" => WrapInline(node, "~~", "~~"),
            "code" => RenderInlineCode(node),
            "a" => RenderLink(node),
            "img" => RenderImage(node),
            "br" => RenderBreak(node),
            _ => throw UnsupportedElement(node)
        };
    }

    /// <summary>Wraps non-empty inline content in a Markdown delimiter pair.</summary>
    private static string WrapInline(HtmlNode node, string opening, string closing)
    {
        EnsureAttributes(node);
        return opening + RenderInlineChildren(node) + closing;
    }

    /// <summary>Selects a delimiter longer than any backtick run and preserves boundary whitespace.</summary>
    private static string RenderInlineCode(HtmlNode node)
    {
        EnsureAttributes(node);
        if (node.Children.Any(child => child.Kind is not HtmlNodeKind.Text))
        {
            throw new MarkdownExportException("Inline code can contain literal text only.");
        }

        var text = string.Concat(node.Children.Select(child => child.Text)).ReplaceLineEndings(" ");
        var delimiter = new string('`', Math.Max(1, LongestRun(text, '`') + 1));
        var needsPadding = text.StartsWith('`') || text.EndsWith('`')
            || text.StartsWith(' ') || text.EndsWith(' ');
        return needsPadding
            ? $"{delimiter} {text} {delimiter}"
            : $"{delimiter}{text}{delimiter}";
    }

    /// <summary>Renders a validated destination and optional title without allowing unsafe Markdown delimiters.</summary>
    private static string RenderLink(HtmlNode node)
    {
        EnsureAttributes(node, ["href", "title"]);
        var href = RequiredAttribute(node, "href");
        EnsureSafeMarkdownDestination(href, "link");
        var title = OptionalTitle(node);
        return $"[{RenderInlineChildren(node)}](<{href}>{title})";
    }

    /// <summary>Renders a childless image with required alternative text and a safe destination.</summary>
    private static string RenderImage(HtmlNode node)
    {
        EnsureAttributes(node, ["src", "alt", "title"]);
        if (node.Children.Count > 0)
        {
            throw new MarkdownExportException("<img> cannot contain children.");
        }

        var source = RequiredAttribute(node, "src");
        EnsureSafeMarkdownDestination(source, "image");
        TryGetAttribute(node, "alt", out var alt);
        var title = OptionalTitle(node);
        return $"![{EscapeText(alt ?? string.Empty)}](<{source}>{title})";
    }

    /// <summary>Renders an attribute-free, childless hard line break.</summary>
    private static string RenderBreak(HtmlNode node)
    {
        EnsureAttributes(node);
        if (node.Children.Count > 0)
        {
            throw new MarkdownExportException("<br> cannot contain children.");
        }

        return "  \n";
    }

    /// <summary>Renders a quoted title only when it can be escaped without changing round-trip semantics.</summary>
    private static string OptionalTitle(HtmlNode node)
    {
        if (!TryGetAttribute(node, "title", out var title))
        {
            return string.Empty;
        }

        if (title is null || title.Contains('\r') || title.Contains('\n'))
        {
            throw new MarkdownExportException("Markdown link and image titles cannot contain line breaks.");
        }

        return $" \"{title.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    /// <summary>Rejects fragment or text nodes where an element-specific renderer was selected.</summary>
    private static void EnsureElement(HtmlNode node)
    {
        if (node.Kind is not HtmlNodeKind.Element || string.IsNullOrWhiteSpace(node.TagName))
        {
            throw new MarkdownExportException("Markdown block content must use supported HTML elements.");
        }
    }

    /// <summary>Rejects style intent and theme classes because Markdown cannot preserve them losslessly.</summary>
    private static void EnsureNoPresentation(HtmlNode node)
    {
        if (node.Style is not null || node.ThemeClasses.Count > 0)
        {
            throw new MarkdownExportException(
                $"<{node.TagName}> contains style or theme-class information that Markdown cannot preserve.");
        }
    }

    /// <summary>Rejects every attribute not explicitly representable by the selected Markdown construct.</summary>
    private static void EnsureAttributes(HtmlNode node, IReadOnlyCollection<string>? allowed = null)
    {
        allowed ??= Array.Empty<string>();
        var unsupported = node.Attributes.Keys.FirstOrDefault(name =>
            !allowed.Contains(name, StringComparer.OrdinalIgnoreCase));
        if (unsupported is not null)
        {
            throw new MarkdownExportException(
                $"The {unsupported} attribute on <{node.TagName}> cannot be represented losslessly in Markdown.");
        }
    }

    /// <summary>Gets a required non-empty attribute or fails the export atomically.</summary>
    private static string RequiredAttribute(HtmlNode node, string name) =>
        TryGetAttribute(node, name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new MarkdownExportException($"<{node.TagName}> requires a {name} attribute for Markdown export.");

    /// <summary>
    /// Returns the first case-insensitive attribute match without detecting duplicate case
    /// variants. The completed export is still accepted only after semantic round-trip
    /// verification.
    /// </summary>
    private static bool TryGetAttribute(HtmlNode node, string name, out string? value)
    {
        foreach (var attribute in node.Attributes)
        {
            if (attribute.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = attribute.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>Determines whether a node belongs to the exporter's supported inline subset.</summary>
    private static bool IsInlineNode(HtmlNode node) =>
        node.Kind is HtmlNodeKind.Text
        || node.Kind is HtmlNodeKind.Element
        && node.TagName is "strong" or "em" or "del" or "code" or "a" or "img" or "br";

    /// <summary>Escapes Markdown punctuation while preserving literal text content.</summary>
    private static string EscapeText(string value)
    {
        var writer = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '&')
            {
                writer.Append("&amp;");
            }
            else if (character == '<')
            {
                writer.Append("&lt;");
            }
            else if (character is '\\' or '`' or '*' or '_' or '{' or '}' or '[' or ']'
                     or '#' or '>' or '+' or '-' or '!' or '~')
            {
                writer.Append('\\').Append(character);
            }
            else if (character == '.'
                     && index > 0
                     && char.IsAsciiDigit(value[index - 1])
                     && (index + 1 == value.Length || char.IsWhiteSpace(value[index + 1])))
            {
                writer.Append("\\.");
            }
            else
            {
                writer.Append(character);
            }
        }

        return writer.ToString();
    }

    /// <summary>Rejects destination characters whose Markdown parsing could change URL meaning.</summary>
    private static void EnsureSafeMarkdownDestination(string value, string kind)
    {
        if (value.Contains('<') || value.Contains('>') || value.Contains('\r') || value.Contains('\n'))
        {
            throw new MarkdownExportException(
                $"The {kind} URL contains characters that cannot be represented safely in Markdown.");
        }
    }

    /// <summary>Validates the bounded language-token grammar used after a fenced-code delimiter.</summary>
    private static bool IsLanguageName(string value) =>
        value.Length > 0
        && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '-' or '_' or '+' or '.');

    /// <summary>Finds the longest delimiter run so generated fences can safely exceed it.</summary>
    private static int LongestRun(string value, char character)
    {
        var longest = 0;
        var current = 0;
        foreach (var candidate in value)
        {
            current = candidate == character ? current + 1 : 0;
            longest = Math.Max(longest, current);
        }

        return longest;
    }

    /// <summary>Creates the consistent failure used for HTML outside the lossless Markdown subset.</summary>
    private static MarkdownExportException UnsupportedElement(HtmlNode node) =>
        new($"<{node.TagName}> cannot be represented losslessly in Markdown.");

    /// <summary>Represents an expected fail-closed export rejection.</summary>
    private sealed class MarkdownExportException(string message) : Exception(message);
}
