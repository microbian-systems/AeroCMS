using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Banners;

public sealed class Banner2EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.banners.2";

    public string DisplayName => "Banner 2";

    public string? Description => "Left-aligned hero banner with CTA buttons.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "image";

    public int SortOrder => 61;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Banner2BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Banner2BlockEditor);

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
        return Banner2BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBannerBlock(editorBlock);

    private static Banner2Block ToBannerBlock(EditorBlock editorBlock)
    {
        var title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "Understand user flow and increase conversions");
        var highlight = editorBlock.Highlight;
        var titleHtml = Banner1EditorBlockDefinition.BuildTitleHtml(title, highlight);

        return new Banner2Block
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

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
