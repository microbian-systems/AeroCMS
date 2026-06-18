using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;

namespace Aero.Cms.Shared.Blocks.Rendering;

/// <summary>
/// Converts transitional legacy block nodes into the semantic, HTML-adjacent
/// composition tree used by the current public renderer.
/// </summary>
public static class PageTreeLegacyBlockNormalizer
{
    public static NeoPageNode Normalize(NeoPageNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var normalized = node.Kind == NeoPageNodeKind.Block
            ? NormalizeLegacyBlock(node)
            : Clone(node);

        normalized.Children = normalized.Children
            .Select(Normalize)
            .ToList();

        return normalized;
    }

    private static NeoPageNode NormalizeLegacyBlock(NeoPageNode node)
    {
        var catalogId = node.CatalogId.Trim();
        return catalogId switch
        {
            "hero" or "boring_hero" => CloneAs(
                node,
                catalogId: "boring_hero",
                kind: NeoPageNodeKind.Section,
                properties: new Dictionary<string, JsonElement>
                {
                    ["title"] = JsonSerializer.SerializeToElement(
                        FirstNonEmpty(GetString(node, "title"), GetString(node, "mainText"))),
                    ["summary"] = JsonSerializer.SerializeToElement(
                        FirstNonEmpty(
                            GetString(node, "summary"),
                            GetString(node, "subText"),
                            GetString(node, "description"))),
                    ["backgroundImageUrl"] = JsonSerializer.SerializeToElement(
                        FirstNonEmpty(
                            GetString(node, "backgroundImageUrl"),
                            GetString(node, "backgroundImage"),
                            GetString(node, "src"),
                            GetString(node, "url"))),
                    ["fullWidth"] = JsonSerializer.SerializeToElement(GetBool(node, "fullWidth", true))
                }),
            "content" or "rich_text" => SectionWithChild(
                node,
                RawHtmlChild(FirstNonEmpty(GetString(node, "content"), GetString(node, "html")))),
            "raw_html" or "ui.raw-html" => SectionWithChild(
                node,
                RawHtmlChild(FirstNonEmpty(GetString(node, "content"), GetString(node, "html")))),
            "markdown" => SectionWithChild(
                node,
                RawHtmlChild(FirstNonEmpty(GetString(node, "content"), GetString(node, "markdown")))),
            "text" or "heading" => CloneAs(
                node,
                catalogId: "primitive.heading",
                kind: NeoPageNodeKind.Primitive,
                properties: new Dictionary<string, JsonElement>
                {
                    ["text"] = JsonSerializer.SerializeToElement(
                        FirstNonEmpty(GetString(node, "text"), GetString(node, "title"), GetString(node, "content"))),
                    ["level"] = JsonSerializer.SerializeToElement(GetInt(node, "level", 2))
                }),
            "quote" => CloneAs(
                node,
                catalogId: "primitive.blockquote",
                kind: NeoPageNodeKind.Primitive,
                properties: new Dictionary<string, JsonElement>
                {
                    ["text"] = JsonSerializer.SerializeToElement(
                        FirstNonEmpty(GetString(node, "text"), GetString(node, "content"))),
                    ["citation"] = JsonSerializer.SerializeToElement(
                        FirstNonEmpty(GetString(node, "citation"), GetString(node, "author")))
                }),
            "image" or "media.image" => CloneAs(
                node,
                catalogId: "primitive.image",
                kind: NeoPageNodeKind.Primitive,
                properties: new Dictionary<string, JsonElement>
                {
                    ["url"] = JsonSerializer.SerializeToElement(
                        FirstNonEmpty(GetString(node, "url"), GetString(node, "src"), GetString(node, "backgroundImage"))),
                    ["alt"] = JsonSerializer.SerializeToElement(GetString(node, "alt")),
                    ["caption"] = JsonSerializer.SerializeToElement(GetString(node, "caption"))
                }),
            "video" or "media.video" => CloneAs(
                node,
                catalogId: "primitive.embed",
                kind: NeoPageNodeKind.Primitive,
                properties: new Dictionary<string, JsonElement>
                {
                    ["url"] = JsonSerializer.SerializeToElement(
                        FirstNonEmpty(GetString(node, "url"), GetString(node, "src"))),
                    ["title"] = JsonSerializer.SerializeToElement(GetString(node, "title"))
                }),
            _ => CloneAs(
                node,
                catalogId: "primitive.section",
                kind: NeoPageNodeKind.Section,
                properties: CloneProperties(node.Properties))
        };
    }

    private static NeoPageNode SectionWithChild(NeoPageNode source, NeoPageNode child) =>
        CloneAs(
            source,
            catalogId: "primitive.section",
            kind: NeoPageNodeKind.Section,
            properties: [],
            children: [child]);

    private static NeoPageNode RawHtmlChild(string html) => new()
    {
        NodeId = Guid.NewGuid().ToString("N"),
        CatalogId = "raw_html",
        Kind = NeoPageNodeKind.Primitive,
        Properties = new Dictionary<string, JsonElement>
        {
            ["content"] = JsonSerializer.SerializeToElement(html)
        },
        Children = []
    };

    private static NeoPageNode CloneAs(
        NeoPageNode source,
        string catalogId,
        NeoPageNodeKind kind,
        Dictionary<string, JsonElement> properties,
        List<NeoPageNode>? children = null) => new()
        {
            NodeId = string.IsNullOrWhiteSpace(source.NodeId)
                ? Guid.NewGuid().ToString("N")
                : source.NodeId,
            CatalogId = catalogId,
            Kind = kind,
            Properties = properties,
            Style = source.Style?.DeepClone() ?? new ResponsiveNodeStyle(),
            Children = children ?? source.Children.Select(Clone).ToList()
        };

    private static NeoPageNode Clone(NeoPageNode source) => new()
    {
        NodeId = string.IsNullOrWhiteSpace(source.NodeId)
            ? Guid.NewGuid().ToString("N")
            : source.NodeId,
        CatalogId = source.CatalogId,
        Kind = source.Kind,
        Properties = CloneProperties(source.Properties),
        Style = source.Style?.DeepClone() ?? new ResponsiveNodeStyle(),
        Children = source.Children.Select(Clone).ToList()
    };

    private static Dictionary<string, JsonElement> CloneProperties(
        IReadOnlyDictionary<string, JsonElement> properties) =>
        properties.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.OrdinalIgnoreCase);

    private static string GetString(NeoPageNode node, string name)
    {
        if (!node.Properties.TryGetValue(name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            _ => string.Empty
        };
    }

    private static bool GetBool(NeoPageNode node, string name, bool fallback) =>
        node.Properties.TryGetValue(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
                _ => fallback
            }
            : fallback;

    private static int GetInt(NeoPageNode node, string name, int fallback) =>
        node.Properties.TryGetValue(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt32(out var parsed) => parsed,
                JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
                _ => fallback
            }
            : fallback;

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
