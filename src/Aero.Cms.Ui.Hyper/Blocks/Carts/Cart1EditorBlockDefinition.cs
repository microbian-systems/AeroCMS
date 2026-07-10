using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Carts;

/// <summary>
/// Represents a class for Cart1EditorBlockDefinition.
/// </summary>
public sealed class Cart1EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.carts.1";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Cart 1";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Sidebar cart panel with product list and action links.";

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
public int SortOrder => 129;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Cart1BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Cart1BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Shopping Cart",
            Description = "Sidebar cart panel"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCartBlock(editorBlock);
        return Cart1BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
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
