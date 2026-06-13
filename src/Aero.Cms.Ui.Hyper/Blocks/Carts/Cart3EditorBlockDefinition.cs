using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Carts;

public sealed class Cart3EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.carts.3";

    public string DisplayName => "Cart 3";

    public string? Description => "Full-page cart with quantities, remove, and order summary.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "shopping-cart";

    public int SortOrder => 131;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Cart3BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Cart3BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Shopping Cart",
            Description = "Full-page cart"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCartBlock(editorBlock);
        return Cart3BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCartBlock(editorBlock);

    private static Cart3Block ToCartBlock(EditorBlock editorBlock)
    {
        return new Cart3Block
        {
            CartItemCount = "2",
            ViewCartText = "View my cart",
            ViewCartUrl = "#",
            CheckoutText = "Checkout",
            CheckoutUrl = "#",
            ContinueShoppingText = "Continue shopping",
            ContinueShoppingUrl = "#",
            Subtotal = "£250",
            Vat = "£25",
            Discount = "-£20",
            Total = "£200"
        };
    }
}
