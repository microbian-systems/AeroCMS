using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Neo;
using System.Text.Json;

namespace Aero.Cms.Shared.Blocks.Rendering;

/// <summary>
/// Transitional adapter for legacy <see cref="NeoPageNodeKind.Block"/> nodes.
/// This keeps old saved page trees renderable without registering legacy block
/// definitions in the global editor catalog, where their IDs collide with the
/// modern discoverable definitions.
/// </summary>
internal static class NeoPageNodeLegacyBlockMapper
{
        /// <summary>
    /// TryMap method.
    /// </summary>
public static bool TryMap(NeoPageNode node, out BlockBase block)
    {
        ArgumentNullException.ThrowIfNull(node);

        var editorBlock = NeoPageNodeEditorBlockMapper.ToEditorBlock(node);
        block = node.CatalogId switch
        {
            "boring_hero" => new BoringHeroBlock
            {
                FullWidth = true,
                Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title),
                Summary = FirstNonEmpty(editorBlock.SubText, editorBlock.Description),
                BackgroundImageUrl = editorBlock.BackgroundImage
            },
            "hero" => new HeroBlock
            {
                Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title),
                SubTitle = FirstNonEmpty(editorBlock.SubText, editorBlock.Description),
                CtaText = editorBlock.CtaText,
                CtaUrl = editorBlock.CtaUrl,
                BackgroundImageUrl = editorBlock.BackgroundImage,
                Height = editorBlock.Height,
                FullScreen = editorBlock.FullScreen
            },
            "content" or "rich_text" => new RichTextBlock
            {
                Content = editorBlock.Content
            },
            "raw_html" => new RawHtmlBlock
            {
                Content = editorBlock.Content
            },
            "markdown" => new MarkdownBlock
            {
                Content = editorBlock.Content
            },
            "dynamic_template" => new DynamicTemplateBlock
            {
                DefinitionVersion = 1,
                InlineTemplate = editorBlock.ScribanTemplate,
                Data = ParseJsonDocument(editorBlock.ScribanDataJson)
            },
            "text" or "heading" => new HeadingBlock
            {
                Text = FirstNonEmpty(editorBlock.Title, editorBlock.Content, editorBlock.MainText),
                Level = 2
            },
            "quote" => new QuoteBlock
            {
                Content = editorBlock.Content,
                Author = editorBlock.Author
            },
            "image" => new Aero.Cms.Abstractions.Blocks.ImageBlock
            {
                Url = FirstNonEmpty(editorBlock.Src, editorBlock.Url, editorBlock.BackgroundImage),
                AltText = editorBlock.Alt,
                Caption = editorBlock.Caption
            },
            "video" => new Aero.Cms.Abstractions.Blocks.Neo.VideoBlock
            {
                Src = FirstNonEmpty(editorBlock.Url, editorBlock.Src),
                Autoplay = editorBlock.AutoPlay,
                Controls = true
            },
            "audio" => new Aero.Cms.Abstractions.Blocks.Neo.AudioBlock
            {
                Src = FirstNonEmpty(editorBlock.Src, editorBlock.Url),
                Controls = true
            },
            _ => default!
        };

        return block is not null;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static JsonDocument ParseJsonDocument(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return JsonDocument.Parse("{}");
        }

        try
        {
            return JsonDocument.Parse(value);
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}");
        }
    }
}
