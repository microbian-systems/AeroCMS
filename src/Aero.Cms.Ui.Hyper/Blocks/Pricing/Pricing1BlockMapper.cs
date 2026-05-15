using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Pricing;

public static class Pricing1BlockMapper
{
    public static NeoPageNode ToNode(Pricing1Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.pricing.1",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["description"] = JsonSerializer.SerializeToElement(block.Description),
            ["plans"] = JsonSerializer.SerializeToElement(block.Plans)
        }
    };

    public static Pricing1Block FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Pricing Plans"),
        Description = GetString(node, "description", "Choose the right plan for your team."),
        Plans = node.Properties.TryGetValue("plans", out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<Pricing1Plan>>(element.GetRawText()) ?? Pricing1Block.DefaultPlans.Select(ClonePlan).ToList()
            : Pricing1Block.DefaultPlans.Select(ClonePlan).ToList()
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static Pricing1Plan ClonePlan(Pricing1Plan plan) => new()
    {
        Name = plan.Name,
        Price = plan.Price,
        Period = plan.Period,
        Features = plan.Features.ToList(),
        CtaText = plan.CtaText,
        CtaUrl = plan.CtaUrl,
        Highlighted = plan.Highlighted
    };
}
