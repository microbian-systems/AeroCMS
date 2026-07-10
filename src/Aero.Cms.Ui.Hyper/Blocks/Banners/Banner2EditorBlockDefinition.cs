using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Banners;

/// <summary>
/// Represents a class for Banner2EditorBlockDefinition.
/// </summary>
public sealed class Banner2EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.banners.2";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Banner 2";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Left-aligned hero banner with CTA buttons.";

        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category => "Hyper";

        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public string Kind => "Block";

        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public string IconName => "image";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 61;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Banner2BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Banner2BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
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

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBannerBlock(editorBlock);
        return Banner2BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
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
