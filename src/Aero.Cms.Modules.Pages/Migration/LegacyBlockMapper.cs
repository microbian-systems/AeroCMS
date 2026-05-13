using System.Text.Json;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Neo;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Pages.Migration;

/// <summary>
/// Maps legacy <see cref="BlockBase"/> documents into <see cref="NeoPageNode"/> trees
/// using stable catalog IDs from <see cref="NeoCatalogIds"/>.
/// </summary>
internal sealed class LegacyBlockMapper : ILegacyBlockMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly ILogger<LegacyBlockMapper> _logger;

    public LegacyBlockMapper(ILogger<LegacyBlockMapper> logger)
    {
        _logger = logger;
    }

    public List<NeoPageNode> MapFromBlock(BlockBase block)
    {
        return block.BlockType switch
        {
            "boring_hero" => MapBoringHero((BoringHeroBlock)block),
            "hero" => MapHero((HeroBlock)block),
            "columns" => MapColumns((ColumnsBlock)block),
            "dynamic_template" => MapDynamicTemplate((DynamicTemplateBlock)block),
            "raw_html" => MapRawHtml((RawHtmlBlock)block),
            "carousel" => MapCarousel((CarouselBlock)block),
            "image" => MapImage(block),
            "heading" => MapPrimitive(block, NeoCatalogIds.Separator),    // heading → primitive note
            "quote" => MapPrimitiveBlock(block, "ui.quote"),
            "separator" => [NewNode(NeoCatalogIds.Separator, NeoPageNodeKind.Primitive)],

            // Video blocks map to media.video with platform metadata
            "youtube" => MapVideo(block, "youtube"),
            "vimeo" => MapVideo(block, "vimeo"),
            "twitch" => MapVideo(block, "twitch"),
            "tiktok" => MapVideo(block, "tiktok"),

            // Rich text block — defer to NeoTextBlock or composition
            "rich_text" => MapPrimitiveBlock(block, "ui.rich-text"),
            "content" => MapPrimitiveBlock(block, "ui.rich-text"),

            // Markdown block
            "markdown" => MapMarkdown((MarkdownBlock)block),

            _ => LogAndSkip(block)
        };
    }

    // ── Boring Hero → aero.hero.basic ──────────────────────────

    private List<NeoPageNode> MapBoringHero(BoringHeroBlock b)
    {
        var node = NewNode(NeoCatalogIds.HeroBasic, NeoPageNodeKind.Block);
        SetProps(node, new Dictionary<string, object?>
        {
            ["title"] = b.Title,
            ["summary"] = b.Summary,
            ["backgroundImageUrl"] = b.BackgroundImageUrl,
            ["fullWidth"] = b.FullWidth,
        });
        return [node];
    }

    // ── Hero → aero.hero.01 ────────────────────────────────────

    private List<NeoPageNode> MapHero(HeroBlock b)
    {
        var node = NewNode(NeoCatalogIds.HeroFull, NeoPageNodeKind.Block);
        SetProps(node, new Dictionary<string, object?>
        {
            ["title"] = b.Title,
            ["subTitle"] = b.SubTitle,
            ["ctaText"] = b.CtaText,
            ["ctaUrl"] = b.CtaUrl,
            ["height"] = b.Height,
            ["fullScreen"] = b.FullScreen,
            ["backgroundImageUrl"] = b.BackgroundImageUrl,
            ["altText"] = b.AltText,
            ["overlayOpacity"] = b.OverlayOpacity,
            ["textAlignment"] = b.TextAlignment,
        });
        return [node];
    }

    // ── Columns → layout nodes ─────────────────────────────────

    private List<NeoPageNode> MapColumns(ColumnsBlock b)
    {
        var root = NewNode(NeoCatalogIds.LayoutColumns, NeoPageNodeKind.Container);
        var nodes = new List<NeoPageNode> { root };

        foreach (var col in b.Columns)
        {
            var colNode = NewNode(
                catalogId: "neo.layout.column",
                kind: NeoPageNodeKind.Container);
            SetProp(colNode, "span", col.Span);

            foreach (var child in col.Blocks)
            {
                var childNodes = MapFromBlock(child);
                colNode.Children.AddRange(childNodes);
            }

            root.Children.Add(colNode);
        }

        return nodes;
    }

    // ── Dynamic Template (Scriban) → neo.template.scriban ──────

    private List<NeoPageNode> MapDynamicTemplate(DynamicTemplateBlock b)
    {
        var node = NewNode(NeoCatalogIds.TemplateScriban, NeoPageNodeKind.Block);
        var props = new Dictionary<string, object?>
        {
            ["definitionId"] = b.DefinitionId,
            ["definitionVersion"] = b.DefinitionVersion,
        };
        if (b.InlineTemplate is not null)
            props["inlineTemplate"] = b.InlineTemplate;
        if (b.Data is not null)
            node.Properties["data"] = JsonSerializer.SerializeToElement(b.Data, SerializerOptions);
        SetProps(node, props);
        return [node];
    }

    // ── Raw HTML → ui.raw-html ─────────────────────────────────

    private List<NeoPageNode> MapRawHtml(RawHtmlBlock b)
    {
        var node = NewNode(NeoCatalogIds.RawHtml, NeoPageNodeKind.Component);
        SetProp(node, "content", b.Content);
        return [node];
    }

    // ── Carousel / Gallery → media.gallery ─────────────────────

    private List<NeoPageNode> MapCarousel(CarouselBlock b)
    {
        var node = NewNode(NeoCatalogIds.MediaGallery, NeoPageNodeKind.Component);
        SetProps(node, new Dictionary<string, object?>
        {
            ["items"] = b.Items.Select(i => new Dictionary<string, object?>
            {
                ["mediaId"] = i.ImageMediaId,
                ["caption"] = i.Caption,
                ["altText"] = i.AltText,
                ["actionUrl"] = i.ActionUrl,
            }).ToList(),
            ["controlLocation"] = b.ControlLocation,
            ["showArrows"] = b.ShowArrows,
            ["autoPlay"] = b.AutoPlay,
            ["interval"] = b.Interval,
        });
        return [node];
    }

    // ── Image → media.image ────────────────────────────────────

    private List<NeoPageNode> MapImage(BlockBase b)
    {
        var node = NewNode(NeoCatalogIds.MediaImage, NeoPageNodeKind.Component);
        // ImageBlock properties vary; extract what we can via STJ
        var json = JsonSerializer.Serialize(b, b.GetType(), SerializerOptions);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("src", out var src))
            node.Properties["src"] = src.Clone();
        if (doc.RootElement.TryGetProperty("alt", out var alt))
            node.Properties["alt"] = alt.Clone();
        if (doc.RootElement.TryGetProperty("caption", out var caption))
            node.Properties["caption"] = caption.Clone();
        if (doc.RootElement.TryGetProperty("url", out var url))
            node.Properties["url"] = url.Clone();
        return [node];
    }

    // ── Video blocks → media.video ─────────────────────────────

    private List<NeoPageNode> MapVideo(BlockBase b, string platform)
    {
        var node = NewNode(NeoCatalogIds.MediaVideo, NeoPageNodeKind.Component);
        var json = JsonSerializer.Serialize(b, b.GetType(), SerializerOptions);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("videoId", out var videoId))
            node.Properties["videoId"] = videoId.Clone();
        if (doc.RootElement.TryGetProperty("url", out var url))
            node.Properties["url"] = url.Clone();
        if (doc.RootElement.TryGetProperty("title", out var title))
            node.Properties["title"] = title.Clone();
        SetProp(node, "platform", platform);
        return [node];
    }

    // ── Markdown → ui.markdown ─────────────────────────────────

    private List<NeoPageNode> MapMarkdown(MarkdownBlock b)
    {
        var node = NewNode("ui.markdown", NeoPageNodeKind.Component);
        SetProp(node, "content", b.Content);
        return [node];
    }

    // ── Primitive blocks (generic) ─────────────────────────────

    private List<NeoPageNode> MapPrimitiveBlock(BlockBase b, string catalogId)
    {
        var node = NewNode(catalogId, NeoPageNodeKind.Component);
        var json = JsonSerializer.Serialize(b, b.GetType(), SerializerOptions);
        using var doc = JsonDocument.Parse(json);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Name is "blockType" or "order" or "id" or "createdOn" or "createdBy"
                or "modifiedOn" or "modifiedBy")
                continue;
            node.Properties[prop.Name] = prop.Value.Clone();
        }

        return [node];
    }

    // ── Helpers ────────────────────────────────────────────────

    private static NeoPageNode NewNode(string catalogId, NeoPageNodeKind kind) => new()
    {
        NodeId = Guid.NewGuid().ToString("N")[..12],
        CatalogId = catalogId,
        Kind = kind,
    };

    private static void SetProp(NeoPageNode node, string key, object? value)
    {
        if (value is null) return;
        node.Properties[key] = JsonSerializer.SerializeToElement(value, SerializerOptions);
    }

    private static void SetProps(NeoPageNode node, Dictionary<string, object?> props)
    {
        foreach (var (key, value) in props)
            SetProp(node, key, value);
    }

    private List<NeoPageNode> LogAndSkip(BlockBase block)
    {
        _logger.LogDebug("LegacyBlockMapper: no mapping for block type '{BlockType}' (id={BlockId})",
            block.BlockType, block.Id);
        return [];
    }

    // For primitives that already exist (separator, etc.)
    private static List<NeoPageNode> MapPrimitive(BlockBase b, string catalogId)
    {
        return [NewNode(catalogId, NeoPageNodeKind.Primitive)];
    }
}
