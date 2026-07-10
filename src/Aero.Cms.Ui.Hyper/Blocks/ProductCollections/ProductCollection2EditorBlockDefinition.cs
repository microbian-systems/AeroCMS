using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCollections;

/// <summary>
/// Represents a class for ProductCollection2EditorBlockDefinition.
/// </summary>
public sealed class ProductCollection2EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.product-collections.2";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Product Collections 2";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Four-column product grid with centered header.";

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
public string IconName => "grid";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 115;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(ProductCollection2BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(ProductCollection2BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Product Collection",
            Description = "Lorem ipsum, dolor sit amet consectetur adipisicing elit.",
            FeatureItems = ProductCollection1Block.DefaultProducts.Select(ToEditorItem).ToList()
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToProductCollectionBlock(editorBlock);
        return ProductCollection2BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToProductCollectionBlock(editorBlock);

    private static ProductCollection2Block ToProductCollectionBlock(EditorBlock editorBlock)
    {
        return new ProductCollection2Block
        {
            Title = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, editorBlock.PageTitle, "Product Collection"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Lorem ipsum, dolor sit amet consectetur adipisicing elit."),
            Products = editorBlock.FeatureItems.Count > 0
                ? editorBlock.FeatureItems.Select(ToProductItem).ToList()
                : ProductCollection1Block.DefaultProducts.Select(CloneProduct).ToList()
        };
    }

    private static AeroFeatureItem ToEditorItem(ProductCollectionItem p) => new()
    {
        Title = p.Name,
        Description = p.Price,
        ImageUrl = p.ImageUrl,
        LinkUrl = p.ProductUrl
    };

    private static ProductCollectionItem ToProductItem(AeroFeatureItem f) => new()
    {
        Name = f.Title ?? string.Empty,
        Price = f.Description ?? string.Empty,
        ImageUrl = f.ImageUrl ?? string.Empty,
        ProductUrl = string.IsNullOrWhiteSpace(f.LinkUrl) ? "#" : f.LinkUrl!
    };

    private static ProductCollectionItem CloneProduct(ProductCollectionItem p) => new()
    {
        Name = p.Name,
        Price = p.Price,
        ImageUrl = p.ImageUrl,
        ProductUrl = p.ProductUrl
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
