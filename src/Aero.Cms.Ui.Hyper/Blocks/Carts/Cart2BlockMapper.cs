using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Carts;

public static class Cart2BlockMapper
{
    public static NeoPageNode ToNode(Cart2Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.carts.2",
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
            ["items"] = JsonSerializer.SerializeToElement(block.Items)
        }
    };

    public static Cart2Block FromNode(NeoPageNode node) => new()
    {
        CartItemCount = GetString(node, "cartItemCount", "2"),
        ViewCartText = GetString(node, "viewCartText", "View my cart"),
        ViewCartUrl = GetString(node, "viewCartUrl", "#"),
        CheckoutText = GetString(node, "checkoutText", "Checkout"),
        CheckoutUrl = GetString(node, "checkoutUrl", "#"),
        ContinueShoppingText = GetString(node, "continueShoppingText", "Continue shopping"),
        ContinueShoppingUrl = GetString(node, "continueShoppingUrl", "#"),
        Items = node.Properties.TryGetValue("items", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<Cart2Item>>(element.GetRawText()) ?? Cart2Block.DefaultItems.Select(CloneItem).ToList()
            : Cart2Block.DefaultItems.Select(CloneItem).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static Cart2Item CloneItem(Cart2Item item) => new()
    {
        Name = item.Name,
        Size = item.Size,
        Color = item.Color,
        ImageSrc = item.ImageSrc,
        Quantity = item.Quantity
    };
}
