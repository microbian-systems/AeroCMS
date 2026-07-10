using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Carts;

/// <summary>
/// Represents a class for Cart3EditorBlockDefinition.
/// </summary>
public sealed class Cart3EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.carts.3";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Cart 3";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Full-page cart with quantities, remove, and order summary.";

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
public string IconName => "shopping-cart";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 131;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Cart3BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Cart3BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Shopping Cart",
            Description = "Full-page cart"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCartBlock(editorBlock);
        return Cart3BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
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
