namespace Aero.Cms.Abstractions.Blocks.Neo;

public static class BasicHeroBlockMapper
{
    public static NeoPageNode ToNode(BasicHeroBlock block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "aero.hero.basic",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(block.Title),
            ["subtitle"] = JsonSerializer.SerializeToElement(block.Subtitle),
            ["backgroundImageUrl"] = JsonSerializer.SerializeToElement(block.BackgroundImageUrl ?? string.Empty),
            ["ctaText"] = JsonSerializer.SerializeToElement(block.CtaText ?? string.Empty),
            ["ctaUrl"] = JsonSerializer.SerializeToElement(block.CtaUrl ?? string.Empty)
        }
    };

    public static BasicHeroBlock FromNode(NeoPageNode node) => new()
    {
        Title = GetString(node, "title", "Welcome"),
        Subtitle = GetString(node, "subtitle", "Your message goes here."),
        BackgroundImageUrl = GetString(node, "backgroundImageUrl", null),
        CtaText = GetString(node, "ctaText", null),
        CtaUrl = GetString(node, "ctaUrl", null)
    };

    private static string? GetString(NeoPageNode node, string key, string? fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString()
            : fallback;
}
