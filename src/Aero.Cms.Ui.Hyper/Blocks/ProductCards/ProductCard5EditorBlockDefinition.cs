using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

/// <summary>
/// Represents a class for ProductCard5EditorBlockDefinition.
/// </summary>
public sealed class ProductCard5EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.product-cards.5";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Product Card 5";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Product card with image, color swatches, title, and price.";

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
public string IconName => "shopping-bag";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 107;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(ProductCard5BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(ProductCard5BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Small Headphones",
            Description = "Space Grey"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCardBlock(editorBlock);
        return ProductCard5BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCardBlock(editorBlock);

    private static ProductCard5Block ToCardBlock(EditorBlock editorBlock)
    {
        return new ProductCard5Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "Small Headphones"),
            Subtitle = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Space Grey"),
            ImageUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1592921870789-04563d55041c?auto=format&fit=crop&q=80&w=1160"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
