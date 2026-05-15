using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Carts;

/// <summary>
/// HyperUI Cart 1 — sidebar cart panel with product list and action links.
/// Source: hyperui/public/examples/marketing/carts/1.html.
/// </summary>
[BlockMetadata(
    "hyper.carts.1",
    "Cart 1",
    Category = "Hyper",
    Icon = "shopping-cart",
    SortOrder = 129,
    SchemaVersion = 1)]
public sealed class Cart1Block : BlockBase
{
    public const string BlockTypeId = "hyper.carts.1";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Shopping Cart";
    public string CartItemCount { get; set; } = "2";
    public string ViewCartText { get; set; } = "View my cart";
    public string ViewCartUrl { get; set; } = "#";
    public string CheckoutText { get; set; } = "Checkout";
    public string CheckoutUrl { get; set; } = "#";
    public string ContinueShoppingText { get; set; } = "Continue shopping";
    public string ContinueShoppingUrl { get; set; } = "#";
    public List<Cart1Item> Items { get; set; } = DefaultItems.Select(CloneItem).ToList();

    public static readonly List<Cart1Item> DefaultItems =
    [
        new() { Name = "Basic Tee 6-Pack", Size = "XXS", Color = "White", ImageSrc = "static.photos/blurred/640x360/110" },
        new() { Name = "Basic Tee 6-Pack", Size = "XXS", Color = "White", ImageSrc = "static.photos/blurred/640x360/111" },
        new() { Name = "Basic Tee 6-Pack", Size = "XXS", Color = "White", ImageSrc = "static.photos/blurred/640x360/112" }
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static Cart1Item CloneItem(Cart1Item item) => new()
    {
        Name = item.Name,
        Size = item.Size,
        Color = item.Color,
        ImageSrc = item.ImageSrc
    };
}

public sealed class Cart1Item
{
    public string Name { get; set; } = "";
    public string Size { get; set; } = "";
    public string Color { get; set; } = "";
    public string ImageSrc { get; set; } = "";
}
