using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Headers;

/// <summary>
/// Represents a class for Header1BlockMapper.
/// </summary>
public static class Header1BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Header1Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.headers.1",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["navLinks"] = JsonSerializer.SerializeToElement(block.NavLinks),
            ["loginUrl"] = JsonSerializer.SerializeToElement(block.LoginUrl),
            ["registerUrl"] = JsonSerializer.SerializeToElement(block.RegisterUrl),
            ["loginText"] = JsonSerializer.SerializeToElement(block.LoginText),
            ["registerText"] = JsonSerializer.SerializeToElement(block.RegisterText)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Header1Block FromNode(NeoPageNode node) => new()
    {
        NavLinks = node.Properties.TryGetValue("navLinks", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<HyperNavLink>>(element.GetRawText()) ?? Header1Block.DefaultNavLinks.Select(CloneNavLink).ToList()
            : Header1Block.DefaultNavLinks.Select(CloneNavLink).ToList(),
        LoginUrl = GetString(node, "loginUrl", "#"),
        RegisterUrl = GetString(node, "registerUrl", "#"),
        LoginText = GetString(node, "loginText", "Login"),
        RegisterText = GetString(node, "registerText", "Register")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static HyperNavLink CloneNavLink(HyperNavLink link) => new()
    {
        Label = link.Label,
        Url = link.Url
    };
}
