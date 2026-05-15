using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

public sealed class ProductCard2EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.product-cards.2";

    public string DisplayName => "Product Card 2";

    public string? Description => "Product card with image hover swap, title, price, and color count.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "shopping-bag";

    public int SortOrder => 104;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(ProductCard2BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(ProductCard2BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Limited Edition Sports Trainer",
            Description = "$189.99"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCardBlock(editorBlock);
        return ProductCard2BlockMapper.ToNode(block);
    }

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
