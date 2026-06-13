namespace Aero.Cms.Abstractions.Blocks.Neo;

public static class NeoRawHtmlBlockMapper
{
    public static NeoPageNode ToNode(NeoRawHtmlBlock block) => new()
    {
        CatalogId = "ui.raw-html", Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["html"] = JsonSerializer.SerializeToElement(block.Html)
        }
    };

    public static NeoRawHtmlBlock FromNode(NeoPageNode node) => new()
    {
        Html = node.Properties.TryGetValue("html", out var v) ? v.GetString() ?? string.Empty : string.Empty
    };
}
