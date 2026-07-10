using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

/// <summary>
/// Represents a class for BlogCard6EditorBlockDefinition.
/// </summary>
public sealed class BlogCard6EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.blog-cards.6";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Blog Card 6";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Horizontal blog card with vertical date, image, and CTA button.";

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
public int SortOrder => 92;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(BlogCard6BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(BlogCard6BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Finding the right guitar for your style - 5 tips",
            Description = "Lorem ipsum dolor sit amet, consectetur adipisicing elit.",
            Src = "https://images.unsplash.com/photo-1609557927087-f9cf8e88de18?auto=format&fit=crop&q=80&w=1160",
            CtaText = "Read Blog",
            CtaUrl = "#"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlogCardBlock(editorBlock);
        return BlogCard6BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlogCardBlock(editorBlock);

    private static BlogCard6Block ToBlogCardBlock(EditorBlock editorBlock)
    {
        return new BlogCard6Block
        {
            ImageUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1609557927087-f9cf8e88de18?auto=format&fit=crop&q=80&w=1160"),
            MainText = FirstNonEmpty(editorBlock.MainText, "Finding the right guitar for your style - 5 tips"),
            Description = FirstNonEmpty(editorBlock.Description, "Lorem ipsum dolor sit amet, consectetur adipisicing elit."),
            PublishedAt = FirstNonEmpty(editorBlock.Title, "2022"),
            PublishedAtDay = FirstNonEmpty(editorBlock.SubText, "Oct 10"),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Read Blog"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
