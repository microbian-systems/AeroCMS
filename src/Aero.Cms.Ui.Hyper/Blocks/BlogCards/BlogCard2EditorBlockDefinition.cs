using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

/// <summary>
/// Represents a class for BlogCard2EditorBlockDefinition.
/// </summary>
public sealed class BlogCard2EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.blog-cards.2";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Blog Card 2";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Blog card with shadow image, title, and description with group hover.";

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
public int SortOrder => 88;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(BlogCard2BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(BlogCard2BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Finding the Journey to Mordor",
            Description = "Lorem ipsum dolor sit amet, consectetur adipisicing elit.",
            Src = "https://images.unsplash.com/photo-1631451095765-2c91616fc9e6?auto=format&fit=crop&q=80&w=1160",
            CtaUrl = "#"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlogCardBlock(editorBlock);
        return BlogCard2BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlogCardBlock(editorBlock);

    private static BlogCard2Block ToBlogCardBlock(EditorBlock editorBlock)
    {
        return new BlogCard2Block
        {
            ImageUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1631451095765-2c91616fc9e6?auto=format&fit=crop&q=80&w=1160"),
            MainText = FirstNonEmpty(editorBlock.MainText, "Finding the Journey to Mordor"),
            Description = FirstNonEmpty(editorBlock.Description, "Lorem ipsum dolor sit amet, consectetur adipisicing elit."),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
