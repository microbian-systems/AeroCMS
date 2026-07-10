using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

/// <summary>
/// Represents a class for BlogCard4EditorBlockDefinition.
/// </summary>
public sealed class BlogCard4EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.blog-cards.4";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Blog Card 4";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Minimal blog card with date, title, and category tags.";

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
public string IconName => "file-text";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 90;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(BlogCard4BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(BlogCard4BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "How to center an element using JavaScript and jQuery",
            SubText = "10th Oct 2022",
            CtaUrl = "#"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlogCardBlock(editorBlock);
        return BlogCard4BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlogCardBlock(editorBlock);

    private static BlogCard4Block ToBlogCardBlock(EditorBlock editorBlock)
    {
        return new BlogCard4Block
        {
            MainText = FirstNonEmpty(editorBlock.MainText, "How to center an element using JavaScript and jQuery"),
            PublishedAt = FirstNonEmpty(editorBlock.SubText, "10th Oct 2022"),
            Tags = editorBlock.FeatureItems.Count > 0
                ? editorBlock.FeatureItems.Select(f => f.Title ?? "").Where(t => !string.IsNullOrEmpty(t)).ToList()
                : ["Snippet", "JavaScript"],
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
