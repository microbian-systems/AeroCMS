using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Pricing;

public static class Pricing2BlockMapper
{
    public static NeoPageNode ToNode(Pricing2Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.pricing.2",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["plans"] = JsonSerializer.SerializeToElement(block.Plans)
        }
    };

    public static Pricing2Block FromNode(NeoPageNode node) => new()
    {
        Plans = node.Properties.TryGetValue("plans", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<Pricing2Plan>>(element.GetRawText()) ?? Pricing2Block.DefaultPlans.Select(ClonePlan).ToList()
            : Pricing2Block.DefaultPlans.Select(ClonePlan).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static Pricing2Plan ClonePlan(Pricing2Plan plan) => new()
    {
        Name = plan.Name,
        Description = plan.Description,
        Price = plan.Price,
        Period = plan.Period,
        CtaText = plan.CtaText,
        CtaUrl = plan.CtaUrl,
        Features = plan.Features.Select(f => new Pricing2Feature { Text = f.Text, Included = f.Included }).ToList()
    };
}
