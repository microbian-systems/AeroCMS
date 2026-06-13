using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Carts;

public sealed class Cart2EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.carts.2";

    public string DisplayName => "Cart 2";

    public string? Description => "Sidebar cart with quantity inputs and remove buttons.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "shopping-cart";

    public int SortOrder => 130;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Cart2BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Cart2BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Shopping Cart",
            Description = "Sidebar cart with quantities"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCartBlock(editorBlock);
        return Cart2BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCartBlock(editorBlock);

    private static Cart2Block ToCartBlock(EditorBlock editorBlock)
    {
        return new Cart2Block
        {
            CartItemCount = "2",
            ViewCartText = "View my cart",
            ViewCartUrl = "#",
            CheckoutText = "Checkout",
            CheckoutUrl = "#",
            ContinueShoppingText = "Continue shopping",
            ContinueShoppingUrl = "#"
        };
    }
}
