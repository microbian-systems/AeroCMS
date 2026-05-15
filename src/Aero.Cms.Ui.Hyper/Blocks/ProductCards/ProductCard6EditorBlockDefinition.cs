using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

public sealed class ProductCard6EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.product-cards.6";

    public string DisplayName => "Product Card 6";

    public string? Description => "Product card with wishlist button, image hover zoom, badge, title, price, add to cart.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "shopping-bag";

    public int SortOrder => 108;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(ProductCard6BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(ProductCard6BlockEditor);

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

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCardBlock(editorBlock);
        return ProductCard6BlockMapper.ToNode(block);
    }

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
