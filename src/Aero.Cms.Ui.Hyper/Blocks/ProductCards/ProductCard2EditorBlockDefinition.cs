using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

/// <summary>
/// Represents a class for ProductCard2EditorBlockDefinition.
/// </summary>
public sealed class ProductCard2EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.product-cards.2";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Product Card 2";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Product card with image hover swap, title, price, and color count.";

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
public int SortOrder => 104;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(ProductCard2BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(ProductCard2BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Limited Edition Sports Trainer",
            Description = "$189.99"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCardBlock(editorBlock);
        return ProductCard2BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCardBlock(editorBlock);

    private static ProductCard2Block ToCardBlock(EditorBlock editorBlock)
    {
        return new ProductCard2Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "Limited Edition Sports Trainer"),
            Price = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "$189.99"),
            ImageUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1600185365483-26d7a4cc7519?auto=format&fit=crop&q=80&w=1160"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
