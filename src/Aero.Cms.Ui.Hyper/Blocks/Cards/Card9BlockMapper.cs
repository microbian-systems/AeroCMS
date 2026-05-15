using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

public static class Card9BlockMapper
{
    public static NeoPageNode ToNode(Card9Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.cards.9",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["avatarUrl"] = JsonSerializer.SerializeToElement(block.AvatarUrl),
            ["commentCount"] = JsonSerializer.SerializeToElement(block.CommentCount),
            ["postedBy"] = JsonSerializer.SerializeToElement(block.PostedBy),
            ["solved"] = JsonSerializer.SerializeToElement(block.Solved),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

    public static Card9Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Question about Rendering"),
        Description = GetString(node, "description", "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Accusamus, accusantium temporibus iure delectus ut totam natus nesciunt ex? Ducimus, enim."),
        AvatarUrl = GetString(node, "avatarUrl", "https://images.unsplash.com/photo-1570295999919-56ceb5ecca61?auto=format&fit=crop&q=80&w=1160"),
        CommentCount = GetInt(node, "commentCount", 14),
        PostedBy = GetString(node, "postedBy", "John"),
        Solved = GetBool(node, "solved", true),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static int GetInt(NeoPageNode node, string key, int fallback) =>
        node.Properties.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : fallback;

    private static bool GetBool(NeoPageNode node, string key, bool fallback) =>
        node.Properties.TryGetValue(key, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : fallback;
}
