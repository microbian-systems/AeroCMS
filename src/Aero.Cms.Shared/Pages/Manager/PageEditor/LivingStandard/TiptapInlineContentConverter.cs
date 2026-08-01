using System.Net;
using System.Text;
using System.Text.Json;
using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Anti-corruption boundary between Tiptap's ProseMirror document and Aero's
/// living-standard phrasing-content tree. Unsupported nodes and marks fail
/// closed instead of leaking editor-specific JSON into the page aggregate.
/// </summary>
public sealed class TiptapInlineContentConverter
{
    private static readonly HashSet<string> EditableTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "strong", "em", "s", "code", "a", "br"
    };

    public bool CanEdit(HtmlNode node) =>
        node.Kind is HtmlNodeKind.Element
        && node.Children.All(child => IsEditableNode(child, insideMark: false));

    public Result<string> ToEditorHtml(HtmlNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (!CanEdit(node))
        {
            return AeroError.NotAllowedError("This element contains content that the inline rich-text editor cannot safely represent.");
        }

        var writer = new StringBuilder("<p>");
        foreach (var child in node.Children)
        {
            WriteNode(child, writer);
        }

        writer.Append("</p>");
        return writer.ToString();
    }

    public Result<IReadOnlyList<HtmlNode>> FromDocumentJson(string documentJson)
    {
        if (string.IsNullOrWhiteSpace(documentJson))
        {
            return AeroError.ValidationError(["The rich-text editor returned an empty document."]);
        }

        try
        {
            using var document = JsonDocument.Parse(documentJson);
            var root = document.RootElement;
            if (!HasType(root, "doc"))
            {
                return AeroError.ValidationError(["The rich-text document must have a ProseMirror doc root."]);
            }

            var children = new List<HtmlNode>();
            if (!root.TryGetProperty("content", out var blocks))
            {
                return new Result<IReadOnlyList<HtmlNode>>.Ok(children);
            }

            if (blocks.ValueKind is not JsonValueKind.Array)
            {
                return AeroError.ValidationError(["The rich-text document content must be an array."]);
            }

            var blockIndex = 0;
            foreach (var block in blocks.EnumerateArray())
            {
                if (!HasType(block, "paragraph"))
                {
                    return AeroError.ValidationError(["Inline rich text supports paragraphs and line breaks only."]);
                }

                if (blockIndex++ > 0)
                {
                    children.Add(HtmlNode.CreateElement("br"));
                }

                var blockResult = ReadInlineChildren(block, children);
                if (blockResult is Result<bool>.Failure failure)
                {
                    return failure.Error;
                }
            }

            return new Result<IReadOnlyList<HtmlNode>>.Ok(children);
        }
        catch (JsonException exception)
        {
            return AeroError.ValidationError([$"The rich-text document is invalid JSON: {exception.Message}"]);
        }
    }

    private static Result<bool> ReadInlineChildren(JsonElement parent, ICollection<HtmlNode> destination)
    {
        if (!parent.TryGetProperty("content", out var content))
        {
            return true;
        }

        if (content.ValueKind is not JsonValueKind.Array)
        {
            return AeroError.ValidationError(["Rich-text node content must be an array."]);
        }

        foreach (var item in content.EnumerateArray())
        {
            var type = TypeOf(item);
            switch (type)
            {
                case "text":
                    var text = item.TryGetProperty("text", out var textValue)
                        ? textValue.GetString() ?? string.Empty
                        : string.Empty;
                    var marked = ApplyMarks(HtmlNode.CreateText(text), item);
                    if (marked is Result<HtmlNode>.Failure markFailure)
                    {
                        return markFailure.Error;
                    }

                    destination.Add(((Result<HtmlNode>.Ok)marked).Value);
                    break;
                case "hardBreak":
                    destination.Add(HtmlNode.CreateElement("br"));
                    break;
                default:
                    return AeroError.ValidationError([$"The rich-text node type '{type ?? "unknown"}' is not supported inline."]);
            }
        }

        return true;
    }

    private static Result<HtmlNode> ApplyMarks(HtmlNode node, JsonElement source)
    {
        if (!source.TryGetProperty("marks", out var marks))
        {
            return node;
        }

        if (marks.ValueKind is not JsonValueKind.Array)
        {
            return AeroError.ValidationError(["Text marks must be an array."]);
        }

        var markItems = marks.EnumerateArray().ToArray();
        if (markItems.Length > 1
            && markItems.Any(mark => string.Equals(TypeOf(mark), "code", StringComparison.Ordinal)))
        {
            return AeroError.ValidationError(["Inline code cannot be combined with other rich-text marks."]);
        }

        foreach (var mark in markItems.Reverse())
        {
            var wrapper = TypeOf(mark) switch
            {
                "bold" => HtmlNode.CreateElement("strong"),
                "italic" => HtmlNode.CreateElement("em"),
                "strike" => HtmlNode.CreateElement("s"),
                "code" => HtmlNode.CreateElement("code"),
                "link" => CreateLink(mark),
                var unsupported => null
            };

            if (wrapper is null)
            {
                return AeroError.ValidationError([$"The rich-text mark '{TypeOf(mark) ?? "unknown"}' is not supported."]);
            }

            wrapper.Children.Add(node);
            node = wrapper;
        }

        return node;
    }

    private static HtmlNode CreateLink(JsonElement mark)
    {
        var link = HtmlNode.CreateElement("a");
        if (!mark.TryGetProperty("attrs", out var attributes)
            || attributes.ValueKind is not JsonValueKind.Object)
        {
            return link;
        }

        CopyStringAttribute(attributes, link, "href");
        CopyStringAttribute(attributes, link, "target");
        CopyStringAttribute(attributes, link, "rel");
        return link;
    }

    private static void CopyStringAttribute(JsonElement source, HtmlNode destination, string name)
    {
        if (source.TryGetProperty(name, out var value)
            && value.ValueKind is JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            destination.Attributes[name] = value.GetString()!;
        }
    }

    private static bool IsEditableNode(HtmlNode node, bool insideMark)
    {
        if (node.Kind is HtmlNodeKind.Text)
        {
            return true;
        }

        if (node.Kind is not HtmlNodeKind.Element
            || node.TagName is null
            || !EditableTags.Contains(node.TagName)
            || !node.Attributes.Keys.All(IsEditableAttribute))
        {
            return false;
        }

        if (node.TagName.Equals("code", StringComparison.OrdinalIgnoreCase))
        {
            return !insideMark && node.Children.All(child => child.Kind is HtmlNodeKind.Text);
        }

        var childIsInsideMark = insideMark
            || !node.TagName.Equals("br", StringComparison.OrdinalIgnoreCase);
        return node.Children.All(child => IsEditableNode(child, childIsInsideMark));
    }

    private static bool IsEditableAttribute(string name) =>
        name.Equals("href", StringComparison.OrdinalIgnoreCase)
        || name.Equals("target", StringComparison.OrdinalIgnoreCase)
        || name.Equals("rel", StringComparison.OrdinalIgnoreCase);

    private static void WriteNode(HtmlNode node, StringBuilder writer)
    {
        if (node.Kind is HtmlNodeKind.Text)
        {
            writer.Append(WebUtility.HtmlEncode(node.Text ?? string.Empty));
            return;
        }

        var tag = node.TagName!;
        writer.Append('<').Append(tag);
        foreach (var (name, value) in node.Attributes.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            writer.Append(' ').Append(name).Append("=\"")
                .Append(WebUtility.HtmlEncode(value)).Append('"');
        }

        writer.Append('>');
        if (tag.Equals("br", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var child in node.Children)
        {
            WriteNode(child, writer);
        }

        writer.Append("</").Append(tag).Append('>');
    }

    private static bool HasType(JsonElement element, string expected) =>
        string.Equals(TypeOf(element), expected, StringComparison.Ordinal);

    private static string? TypeOf(JsonElement element) =>
        element.TryGetProperty("type", out var type) && type.ValueKind is JsonValueKind.String
            ? type.GetString()
            : null;
}
