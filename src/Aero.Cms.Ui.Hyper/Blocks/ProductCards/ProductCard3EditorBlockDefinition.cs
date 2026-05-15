using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ProductCards;

public sealed class ProductCard3EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.product-cards.3";

    public string DisplayName => "Product Card 3";

    public string? Description => "Product card with image, title, description, and price.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "shopping-bag";

    public int SortOrder => 105;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(ProductCard3BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(ProductCard3BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Small Headphones",
            Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Quasi nobis, quia soluta quisquam voluptatem nemo."
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCardBlock(editorBlock);
        return ProductCard3BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCardBlock(editorBlock);

    private static ProductCard3Block ToCardBlock(EditorBlock editorBlock)
    {
        return new ProductCard3Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "Small Headphones"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Lorem ipsum dolor sit amet consectetur adipisicing elit. Quasi nobis, quia soluta quisquam voluptatem nemo."),
            Price = FirstNonEmpty(string.Empty, "$299"),
            ImageUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1592921870789-04563d55041c?auto=format&fit=crop&q=80&w=1160"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
