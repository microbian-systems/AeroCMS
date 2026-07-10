using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// Represents a class for Footer9BlockMapper.
/// </summary>
public static class Footer9BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Footer9Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.footers.9",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["navLinks"] = JsonSerializer.SerializeToElement(block.NavLinks),
            ["copyright"] = JsonSerializer.SerializeToElement(block.Copyright)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Footer9Block FromNode(NeoPageNode node) => new()
    {
        Description = GetString(node, "description", "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Incidunt consequuntur amet culpa cum itaque neque."),
        Copyright = GetString(node, "copyright", "Copyright &copy; 2022. All rights reserved."),
        NavLinks = node.Properties.TryGetValue("navLinks", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<FooterLink>>(element.GetRawText()) ?? Footer9Block.DefaultNavLinks.Select(CloneLink).ToList()
            : Footer9Block.DefaultNavLinks.Select(CloneLink).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static FooterLink CloneLink(FooterLink link) => new()
    {
        Text = link.Text,
        Url = link.Url
    };
}
