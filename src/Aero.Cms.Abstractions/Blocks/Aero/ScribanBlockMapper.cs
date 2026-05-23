namespace Aero.Cms.Abstractions.Blocks.Neo;

public static class ScribanBlockMapper
{
    public static NeoPageNode ToNode(ScribanBlock block) => new()
    {
        CatalogId = "neo.template.scriban", Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement(block.Name),
            ["template"] = JsonSerializer.SerializeToElement(block.Template),
            ["data"] = block.Data is not null
                ? JsonSerializer.SerializeToElement(block.Data.RootElement.GetRawText())
                : JsonSerializer.SerializeToElement("{}")
        }
    };

    public static ScribanBlock FromNode(NeoPageNode node) => new()
    {
        Name = node.Properties.TryGetValue("name", out var n) ? n.GetString() ?? "Scriban Block" : "Scriban Block",
        Template = node.Properties.TryGetValue("template", out var t) ? t.GetString() ?? string.Empty : string.Empty,
        Data = node.Properties.TryGetValue("data", out var d) &&
               d.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(d.GetString())
            ? JsonDocument.Parse(d.GetString()!)
            : null
    };
}
