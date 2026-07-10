using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

/// <summary>
/// Represents a class for Card1BlockMapper.
/// </summary>
public static class Card1BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Card1Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.cards.1",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["authorName"] = JsonSerializer.SerializeToElement(block.AuthorName),
            ["avatarUrl"] = JsonSerializer.SerializeToElement(block.AvatarUrl),
            ["dateStr"] = JsonSerializer.SerializeToElement(block.DateStr),
            ["readingTime"] = JsonSerializer.SerializeToElement(block.ReadingTime),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Card1Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "How I built my first website with Nuxt, Tailwind CSS and Vercel"),
        Description = GetString(node, "description", "Lorem ipsum dolor sit, amet consectetur adipisicing elit. At velit illum provident a, ipsa maiores deleniti consectetur nobis et eaque."),
        AuthorName = GetString(node, "authorName", "John Doe"),
        AvatarUrl = GetString(node, "avatarUrl", "https://images.unsplash.com/photo-1633332755192-727a05c4013d?auto=format&fit=crop&q=80&w=1160"),
        DateStr = GetString(node, "dateStr", "31/06/2025"),
        ReadingTime = GetString(node, "readingTime", "12 minutes"),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
