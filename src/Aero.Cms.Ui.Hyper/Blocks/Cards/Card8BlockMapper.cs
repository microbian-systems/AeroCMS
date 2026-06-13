using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

public static class Card8BlockMapper
{
    public static NeoPageNode ToNode(Card8Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.cards.8",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["episodeBadge"] = JsonSerializer.SerializeToElement(block.EpisodeBadge),
            ["duration"] = JsonSerializer.SerializeToElement(block.Duration),
            ["featuring"] = JsonSerializer.SerializeToElement(block.Featuring),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl)
        }
    };

    public static Card8Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Some Interesting Podcast Title"),
        Description = GetString(node, "description", "Lorem ipsum, dolor sit amet consectetur adipisicing elit. Ipsam nulla amet voluptatum sit rerum, atque, quo culpa ut necessitatibus eius suscipit eum accusamus, aperiam voluptas exercitationem facere aliquid fuga. Sint."),
        EpisodeBadge = GetString(node, "episodeBadge", "Episode #101"),
        Duration = GetString(node, "duration", "48:32 minutes"),
        Featuring = GetString(node, "featuring", "Barry, Sandra and August"),
        CtaUrl = GetString(node, "ctaUrl", "#")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
