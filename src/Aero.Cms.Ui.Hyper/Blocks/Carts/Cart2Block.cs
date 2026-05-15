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
    public const string BlockTypeId = "hyper.carts.2";

    public override string BlockType => BlockTypeId;

    public string CartItemCount { get; set; } = "2";
    public string ViewCartText { get; set; } = "View my cart";
    public string ViewCartUrl { get; set; } = "#";
    public string CheckoutText { get; set; } = "Checkout";
    public string CheckoutUrl { get; set; } = "#";
    public string ContinueShoppingText { get; set; } = "Continue shopping";
    public string ContinueShoppingUrl { get; set; } = "#";
    public List<Cart2Item> Items { get; set; } = DefaultItems.Select(CloneItem).ToList();

    public static readonly List<Cart2Item> DefaultItems =
    [
        new() { Name = "Basic Tee 6-Pack", Size = "XXS", Color = "White", ImageSrc = "static.photos/blurred/640x360/110", Quantity = 1 },
        new() { Name = "Basic Tee 6-Pack", Size = "XXS", Color = "White", ImageSrc = "static.photos/blurred/640x360/111", Quantity = 1 },
        new() { Name = "Basic Tee 6-Pack", Size = "XXS", Color = "White", ImageSrc = "static.photos/blurred/640x360/112", Quantity = 2 }
    ];

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

public sealed class Cart2Item
{
    public string Name { get; set; } = "";
    public string Size { get; set; } = "";
    public string Color { get; set; } = "";
    public string ImageSrc { get; set; } = "";
    public int Quantity { get; set; } = 1;
}
