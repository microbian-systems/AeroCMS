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
    public const string BlockTypeId = "hyper.carts.3";

    public override string BlockType => BlockTypeId;

    public string CartItemCount { get; set; } = "2";
    public string ViewCartText { get; set; } = "View my cart";
    public string ViewCartUrl { get; set; } = "#";
    public string CheckoutText { get; set; } = "Checkout";
    public string CheckoutUrl { get; set; } = "#";
    public string ContinueShoppingText { get; set; } = "Continue shopping";
    public string ContinueShoppingUrl { get; set; } = "#";
    public string Subtotal { get; set; } = "£250";
    public string Vat { get; set; } = "£25";
    public string Discount { get; set; } = "-£20";
    public string Total { get; set; } = "£200";
    public List<Cart3Item> Items { get; set; } = DefaultItems.Select(CloneItem).ToList();

    public static readonly List<Cart3Item> DefaultItems =
    [
        new() { Name = "Basic Tee 6-Pack", Size = "XXS", Color = "White", ImageSrc = "static.photos/blurred/640x360/110", Quantity = 1 },
        new() { Name = "Basic Tee 6-Pack", Size = "XXS", Color = "White", ImageSrc = "static.photos/blurred/640x360/111", Quantity = 1 },
        new() { Name = "Basic Tee 6-Pack", Size = "XXS", Color = "White", ImageSrc = "static.photos/blurred/640x360/112", Quantity = 2 }
    ];

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

public sealed class Cart3Item
{
    public string Name { get; set; } = "";
    public string Size { get; set; } = "";
    public string Color { get; set; } = "";
    public string ImageSrc { get; set; } = "";
    public int Quantity { get; set; } = 1;
}
