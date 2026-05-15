using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Carts;

public static class Cart3BlockMapper
{
    public static NeoPageNode ToNode(Cart3Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.carts.3",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["cartItemCount"] = JsonSerializer.SerializeToElement(block.CartItemCount),
            ["viewCartText"] = JsonSerializer.SerializeToElement(block.ViewCartText),
            ["viewCartUrl"] = JsonSerializer.SerializeToElement(block.ViewCartUrl),
            ["checkoutText"] = JsonSerializer.SerializeToElement(block.CheckoutText),
            ["checkoutUrl"] = JsonSerializer.SerializeToElement(block.CheckoutUrl),
            ["continueShoppingText"] = JsonSerializer.SerializeToElement(block.ContinueShoppingText),
            ["continueShoppingUrl"] = JsonSerializer.SerializeToElement(block.ContinueShoppingUrl),
            ["subtotal"] = JsonSerializer.SerializeToElement(block.Subtotal),
            ["vat"] = JsonSerializer.SerializeToElement(block.Vat),
            ["discount"] = JsonSerializer.SerializeToElement(block.Discount),
            ["total"] = JsonSerializer.SerializeToElement(block.Total),
            ["items"] = JsonSerializer.SerializeToElement(block.Items)
        }
    };

    public static Cart3Block FromNode(NeoPageNode node) => new()
    {
        CartItemCount = GetString(node, "cartItemCount", "2"),
        ViewCartText = GetString(node, "viewCartText", "View my cart"),
        ViewCartUrl = GetString(node, "viewCartUrl", "#"),
        CheckoutText = GetString(node, "checkoutText", "Checkout"),
        CheckoutUrl = GetString(node, "checkoutUrl", "#"),
        ContinueShoppingText = GetString(node, "continueShoppingText", "Continue shopping"),
        ContinueShoppingUrl = GetString(node, "continueShoppingUrl", "#"),
        Subtotal = GetString(node, "subtotal", "£250"),
        Vat = GetString(node, "vat", "£25"),
        Discount = GetString(node, "discount", "-£20"),
        Total = GetString(node, "total", "£200"),
        Items = node.Properties.TryGetValue("items", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<Cart3Item>>(element.GetRawText()) ?? Cart3Block.DefaultItems.Select(CloneItem).ToList()
            : Cart3Block.DefaultItems.Select(CloneItem).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static Cart3Item CloneItem(Cart3Item item) => new()
    {
        Name = item.Name,
        Size = item.Size,
        Color = item.Color,
        ImageSrc = item.ImageSrc,
        Quantity = item.Quantity
    };
}
