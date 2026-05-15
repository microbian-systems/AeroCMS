using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

public sealed class ProductCard4EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.product-cards.4";

    public string DisplayName => "Product Card 4";

    public string? Description => "Simple product card with image, title, and price.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "shopping-bag";

    public int SortOrder => 106;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(ProductCard4BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(ProductCard4BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Simple Watch",
            Description = "$150"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCardBlock(editorBlock);
        return ProductCard4BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCardBlock(editorBlock);

    private static ProductCard4Block ToCardBlock(EditorBlock editorBlock)
    {
        return new ProductCard4Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "Simple Watch"),
            Price = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "$150"),
            ImageUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1592921870789-04563d55041c?auto=format&fit=crop&q=80&w=1160"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
