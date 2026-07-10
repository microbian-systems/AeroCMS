using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Carts;

/// <summary>
/// Represents a class for Cart2EditorBlockDefinition.
/// </summary>
public sealed class Cart2EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.carts.2";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Cart 2";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Sidebar cart with quantity inputs and remove buttons.";

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
public int SortOrder => 130;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Cart2BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Cart2BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Shopping Cart",
            Description = "Sidebar cart with quantities"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCartBlock(editorBlock);
        return Cart2BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
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
