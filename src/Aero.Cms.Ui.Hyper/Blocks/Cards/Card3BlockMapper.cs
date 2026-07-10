using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

/// <summary>
/// Represents a class for Card3BlockMapper.
/// </summary>
public static class Card3BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Card3Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.cards.3",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["subtitle"] = JsonSerializer.SerializeToElement(block.Subtitle),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Card3Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Tony Wayne"),
        Subtitle = GetString(node, "subtitle", "Developer"),
        Description = GetString(node, "description", "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Omnis perferendis hic asperiores quibusdam quidem voluptates doloremque reiciendis nostrum harum. Repudiandae?"),
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1603871165848-0aa92c869fa1?auto=format&fit=crop&q=80&w=1160"),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
