using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Headers;

public static class Header4BlockMapper
{
    public static NeoPageNode ToNode(Header4Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.headers.4",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["navLinks"] = JsonSerializer.SerializeToElement(block.NavLinks),
            ["userAvatarUrl"] = JsonSerializer.SerializeToElement(block.UserAvatarUrl),
            ["userMenuItems"] = JsonSerializer.SerializeToElement(block.UserMenuItems),
            ["logoutUrl"] = JsonSerializer.SerializeToElement(block.LogoutUrl),
            ["logoutText"] = JsonSerializer.SerializeToElement(block.LogoutText)
        }
    };

    public static Header4Block FromNode(NeoPageNode node) => new()
    {
        NavLinks = node.Properties.TryGetValue("navLinks", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<HyperNavLink>>(element.GetRawText()) ?? Header4Block.DefaultNavLinks.Select(CloneNavLink).ToList()
            : Header4Block.DefaultNavLinks.Select(CloneNavLink).ToList(),
        UserAvatarUrl = GetStringOrNull(node, "userAvatarUrl"),
        UserMenuItems = node.Properties.TryGetValue("userMenuItems", out var menuElement) && menuElement.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<HyperNavLink>>(menuElement.GetRawText()) ?? Header4Block.DefaultUserMenuItems.Select(CloneNavLink).ToList()
            : Header4Block.DefaultUserMenuItems.Select(CloneNavLink).ToList(),
        LogoutUrl = GetStringOrNull(node, "logoutUrl"),
        LogoutText = GetString(node, "logoutText", "Logout")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static string? GetStringOrNull(NeoPageNode node, string key) =>
        node.Properties.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static HyperNavLink CloneNavLink(HyperNavLink link) => new()
    {
        Label = link.Label,
        Url = link.Url
    };
}
