using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

/// <summary>
/// Represents a class for BlogCard3EditorBlockDefinition.
/// </summary>
public sealed class BlogCard3EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.blog-cards.3";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Blog Card 3";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Bordered blog card with image, title, description, and link.";

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
public int SortOrder => 89;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(BlogCard3BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(BlogCard3BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Lorem ipsum dolor sit amet consectetur adipisicing elit.",
            Description = "Lorem ipsum dolor sit amet, consectetur adipisicing elit.",
            Src = "https://images.unsplash.com/photo-1600880292203-757bb62b4baf?auto=format&fit=crop&q=80&w=1160",
            CtaText = "Find out more",
            CtaUrl = "#"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlogCardBlock(editorBlock);
        return BlogCard3BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlogCardBlock(editorBlock);

    private static BlogCard3Block ToBlogCardBlock(EditorBlock editorBlock)
    {
        return new BlogCard3Block
        {
            ImageUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1600880292203-757bb62b4baf?auto=format&fit=crop&q=80&w=1160"),
            MainText = FirstNonEmpty(editorBlock.MainText, "Lorem ipsum dolor sit amet consectetur adipisicing elit."),
            Description = FirstNonEmpty(editorBlock.Description, "Lorem ipsum dolor sit amet, consectetur adipisicing elit."),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Find out more"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
