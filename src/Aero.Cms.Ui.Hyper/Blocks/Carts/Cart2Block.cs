using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Carts;

/// <summary>
/// HyperUI Cart 2 — sidebar cart with quantity inputs and remove buttons.
/// Source: hyperui/public/examples/marketing/carts/2.html.
/// </summary>
[BlockMetadata(
    "hyper.carts.2",
    "Cart 2",
    Category = "Hyper",
    Icon = "shopping-cart",
    SortOrder = 130,
    SchemaVersion = 1)]
public sealed class Cart2Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.carts.2";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Cart Item Count.
    /// </summary>
public string CartItemCount { get; set; } = "2";
        /// <summary>
    /// Gets or sets the View Cart Text.
    /// </summary>
public string ViewCartText { get; set; } = "View my cart";
        /// <summary>
    /// Gets or sets the View Cart Url.
    /// </summary>
public string ViewCartUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Checkout Text.
    /// </summary>
public string CheckoutText { get; set; } = "Checkout";
        /// <summary>
    /// Gets or sets the Checkout Url.
    /// </summary>
public string CheckoutUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Continue Shopping Text.
    /// </summary>
public string ContinueShoppingText { get; set; } = "Continue shopping";
        /// <summary>
    /// Gets or sets the Continue Shopping Url.
    /// </summary>
public string ContinueShoppingUrl { get; set; } = "#";
        /// <summary>
    /// Gets or sets the Items.
    /// </summary>
public List<Cart2Item> Items { get; set; } = DefaultItems.Select(CloneItem).ToList();

        /// <summary>
    /// DefaultItems.
    /// </summary>
public static readonly List<Cart2Item> DefaultItems =
    [
        new() { Name = "Basic Tee 6-Pack", Size = "XXS", Color = "White", ImageSrc = "static.photos/blurred/640x360/110", Quantity = 1 },
        new() { Name = "Basic Tee 6-Pack", Size = "XXS", Color = "White", ImageSrc = "static.photos/blurred/640x360/111", Quantity = 1 },
        new() { Name = "Basic Tee 6-Pack", Size = "XXS", Color = "White", ImageSrc = "static.photos/blurred/640x360/112", Quantity = 2 }
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static Cart2Item CloneItem(Cart2Item item) => new()
    {
        Name = item.Name,
        Size = item.Size,
        Color = item.Color,
        ImageSrc = item.ImageSrc,
        Quantity = item.Quantity
    };
}

/// <summary>
/// Represents a class for Cart2Item.
/// </summary>
public sealed class Cart2Item
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = "";
        /// <summary>
    /// Gets or sets the Size.
    /// </summary>
public string Size { get; set; } = "";
        /// <summary>
    /// Gets or sets the Color.
    /// </summary>
public string Color { get; set; } = "";
        /// <summary>
    /// Gets or sets the Image Src.
    /// </summary>
public string ImageSrc { get; set; } = "";
        /// <summary>
    /// Gets or sets the Quantity.
    /// </summary>
public int Quantity { get; set; } = 1;
}
