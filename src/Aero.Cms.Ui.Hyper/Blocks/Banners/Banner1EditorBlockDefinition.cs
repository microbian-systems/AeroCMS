using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Banners;

public sealed class Banner1EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.banners.1";

    public string DisplayName => "Banner 1";

    public string? Description => "Centered hero banner with CTA buttons.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "image";

    public int SortOrder => 60;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Banner1BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Banner1BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Understand user flow and increase conversions",
            Highlight = "increase",
            Description = "Lorem ipsum dolor sit amet, consectetur adipisicing elit. Eaque, nisi. Natus, provident accusamus impedit minima harum corporis iusto.",
            CtaText = "Get Started",
            CtaUrl = "#",
            CtaText2 = "Learn More",
            CtaUrl2 = "#"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBannerBlock(editorBlock);
        return Banner1BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBannerBlock(editorBlock);

    private static Banner1Block ToBannerBlock(EditorBlock editorBlock)
    {
        var title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "Understand user flow and increase conversions");
        var highlight = editorBlock.Highlight;
        var titleHtml = BuildTitleHtml(title, highlight);

        return new Banner1Block
        {
            Title = titleHtml,
            Highlight = highlight,
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Lorem ipsum dolor sit amet, consectetur adipisicing elit. Eaque, nisi. Natus, provident accusamus impedit minima harum corporis iusto."),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Get Started"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#"),
            CtaText2 = FirstNonEmpty(editorBlock.CtaText2, "Learn More"),
            CtaUrl2 = FirstNonEmpty(editorBlock.CtaUrl2, "#")
        };
    }

    internal static string BuildTitleHtml(string title, string? highlight)
    {
        if (string.IsNullOrEmpty(highlight))
            return title;

        var idx = title.IndexOf(highlight, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return title;

        var before = title[..idx];
        var after = title[(idx + highlight.Length)..];
        return $"{before}<strong class=\"text-indigo-600\">{highlight}</strong>{after}";
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
