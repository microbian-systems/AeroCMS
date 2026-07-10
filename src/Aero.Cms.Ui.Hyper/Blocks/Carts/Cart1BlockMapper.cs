using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Carts;

/// <summary>
/// Represents a class for Cart1BlockMapper.
/// </summary>
public static class Cart1BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Cart1Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.carts.1",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
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

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Cart1Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Shopping Cart"),
        CartItemCount = GetString(node, "cartItemCount", "2"),
        ViewCartText = GetString(node, "viewCartText", "View my cart"),
        ViewCartUrl = GetString(node, "viewCartUrl", "#"),
        CheckoutText = GetString(node, "checkoutText", "Checkout"),
        CheckoutUrl = GetString(node, "checkoutUrl", "#"),
        ContinueShoppingText = GetString(node, "continueShoppingText", "Continue shopping"),
        ContinueShoppingUrl = GetString(node, "continueShoppingUrl", "#"),
        Items = node.Properties.TryGetValue("items", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<Cart1Item>>(element.GetRawText()) ?? Cart1Block.DefaultItems.Select(CloneItem).ToList()
            : Cart1Block.DefaultItems.Select(CloneItem).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static Cart1Item CloneItem(Cart1Item item) => new()
    {
        Name = item.Name,
        Size = item.Size,
        Color = item.Color,
        ImageSrc = item.ImageSrc
    };
}
