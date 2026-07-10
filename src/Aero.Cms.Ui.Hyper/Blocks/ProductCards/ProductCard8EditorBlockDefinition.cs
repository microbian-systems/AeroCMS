using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

/// <summary>
/// Represents a class for ProductCard8EditorBlockDefinition.
/// </summary>
public sealed class ProductCard8EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.product-cards.8";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Product Card 8";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Product card with wishlist button, image, price with strikethrough, title, description, two CTA buttons.";

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
public int SortOrder => 110;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(ProductCard8BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(ProductCard8BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Wireless Headphones",
            Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Labore nobis iure obcaecati pariatur. Officiis qui, enim cupiditate aliquam corporis iste.",
            CtaText = "Add to Cart",
            CtaUrl = "#",
            CtaText2 = "Buy Now",
            CtaUrl2 = "#"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCardBlock(editorBlock);
        return ProductCard8BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCardBlock(editorBlock);

    private static ProductCard8Block ToCardBlock(EditorBlock editorBlock)
    {
        return new ProductCard8Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "Wireless Headphones"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Lorem ipsum dolor sit amet consectetur adipisicing elit. Labore nobis iure obcaecati pariatur. Officiis qui, enim cupiditate aliquam corporis iste."),
            ImageUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1628202926206-c63a34b1618f?auto=format&fit=crop&q=80&w=1160"),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Add to Cart"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#"),
            CtaText2 = FirstNonEmpty(editorBlock.CtaText2, "Buy Now"),
            CtaUrl2 = FirstNonEmpty(editorBlock.CtaUrl2, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
