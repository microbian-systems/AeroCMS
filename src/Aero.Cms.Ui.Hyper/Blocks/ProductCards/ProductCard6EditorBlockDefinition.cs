using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

/// <summary>
/// Represents a class for ProductCard6EditorBlockDefinition.
/// </summary>
public sealed class ProductCard6EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.product-cards.6";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Product Card 6";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Product card with wishlist button, image hover zoom, badge, title, price, add to cart.";

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
public int SortOrder => 108;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(ProductCard6BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(ProductCard6BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Robot Toy",
            Description = "$14.99",
            CtaText = "Add to Cart"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCardBlock(editorBlock);
        return ProductCard6BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCardBlock(editorBlock);

    private static ProductCard6Block ToCardBlock(EditorBlock editorBlock)
    {
        return new ProductCard6Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "Robot Toy"),
            Price = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "$14.99"),
            ImageUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1599481238640-4c1288750d7a?auto=format&fit=crop&q=80&w=1160"),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Add to Cart"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
