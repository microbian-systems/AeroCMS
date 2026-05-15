using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Carts;

public sealed class Cart1EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.carts.1";

    public string DisplayName => "Cart 1";

    public string? Description => "Sidebar cart panel with product list and action links.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "shopping-cart";

    public int SortOrder => 129;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Cart1BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Cart1BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Shopping Cart",
            Description = "Sidebar cart panel"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCartBlock(editorBlock);
        return Cart1BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCartBlock(editorBlock);

    private static Cart1Block ToCartBlock(EditorBlock editorBlock)
    {
        return new Cart1Block
        {
            Title = FirstNonEmpty(editorBlock.Title, editorBlock.SectionTitle, "Shopping Cart"),
            CartItemCount = "2",
            ViewCartText = "View my cart",
            ViewCartUrl = "#",
            CheckoutText = "Checkout",
            CheckoutUrl = "#",
            ContinueShoppingText = "Continue shopping",
            ContinueShoppingUrl = "#"
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
