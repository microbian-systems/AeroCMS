using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

/// <summary>
/// Represents a class for BlogCard1EditorBlockDefinition.
/// </summary>
public sealed class BlogCard1EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.blog-cards.1";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Blog Card 1";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Classic blog card with image, date, title, and description.";

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
public int SortOrder => 87;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(BlogCard1BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(BlogCard1BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "How to position your furniture for positivity",
            Description = "Lorem ipsum dolor sit amet, consectetur adipisicing elit.",
            Src = "https://images.unsplash.com/photo-1524758631624-e2822e304c36?auto=format&fit=crop&q=80&w=1160",
            CtaUrl = "#"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlogCardBlock(editorBlock);
        return BlogCard1BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlogCardBlock(editorBlock);

    private static BlogCard1Block ToBlogCardBlock(EditorBlock editorBlock)
    {
        return new BlogCard1Block
        {
            ImageUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1524758631624-e2822e304c36?auto=format&fit=crop&q=80&w=1160"),
            MainText = FirstNonEmpty(editorBlock.MainText, "How to position your furniture for positivity"),
            Description = FirstNonEmpty(editorBlock.Description, "Lorem ipsum dolor sit amet, consectetur adipisicing elit."),
            PublishedAt = FirstNonEmpty(editorBlock.SubText, "10th Oct 2022"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
