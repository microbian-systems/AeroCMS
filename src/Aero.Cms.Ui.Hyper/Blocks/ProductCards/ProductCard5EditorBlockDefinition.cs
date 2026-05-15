using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

public sealed class ProductCard5EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.product-cards.5";

    public string DisplayName => "Product Card 5";

    public string? Description => "Product card with image, color swatches, title, and price.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "shopping-bag";

    public int SortOrder => 107;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(ProductCard5BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(ProductCard5BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Small Headphones",
            Description = "Space Grey"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCardBlock(editorBlock);
        return ProductCard5BlockMapper.ToNode(block);
    }

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
