using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Carts;

/// <summary>
/// HyperUI Cart 3 — full-page cart with quantities, remove, and order summary.
/// Source: hyperui/public/examples/marketing/carts/3.html.
/// </summary>
[BlockMetadata(
    "hyper.carts.3",
    "Cart 3",
    Category = "Hyper",
    Icon = "shopping-cart",
    SortOrder = 131,
    SchemaVersion = 1)]
public sealed class Cart3Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.carts.3";

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
    /// Gets or sets the Subtotal.
    /// </summary>
public string Subtotal { get; set; } = "£250";
        /// <summary>
    /// Gets or sets the Vat.
    /// </summary>
public string Vat { get; set; } = "£25";
        /// <summary>
    /// Gets or sets the Discount.
    /// </summary>
public string Discount { get; set; } = "-£20";
        /// <summary>
    /// Gets or sets the Total.
    /// </summary>
public string Total { get; set; } = "£200";
        /// <summary>
    /// Gets or sets the Items.
    /// </summary>
public List<Cart3Item> Items { get; set; } = DefaultItems.Select(CloneItem).ToList();

        /// <summary>
    /// DefaultItems.
    /// </summary>
public static readonly List<Cart3Item> DefaultItems =
    [
        new() { Name = "Basic Tee 6-Pack", Size = "XXS", Color = "White", ImageSrc = "static.photos/blurred/640x360/110", Quantity = 1 },
        new() { Name = "Basic Tee 6-Pack", Size = "XXS", Color = "White", ImageSrc = "static.photos/blurred/640x360/111", Quantity = 1 },
        new() { Name = "Basic Tee 6-Pack", Size = "XXS", Color = "White", ImageSrc = "static.photos/blurred/640x360/112", Quantity = 2 }
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static Cart3Item CloneItem(Cart3Item item) => new()
    {
        Name = item.Name,
        Size = item.Size,
        Color = item.Color,
        ImageSrc = item.ImageSrc,
        Quantity = item.Quantity
    };
}

/// <summary>
/// Represents a class for Cart3Item.
/// </summary>
public sealed class Cart3Item
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
