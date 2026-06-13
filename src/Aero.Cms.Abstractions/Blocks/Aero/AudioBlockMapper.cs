namespace Aero.Cms.Abstractions.Blocks.Neo;

public static class AudioBlockMapper
{
    public static NeoPageNode ToNode(AudioBlock block) => new()
    {
        CatalogId = "media.audio", Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["src"] = JsonSerializer.SerializeToElement(block.Src),
            ["caption"] = JsonSerializer.SerializeToElement(block.Caption ?? string.Empty),
            ["controls"] = JsonSerializer.SerializeToElement(block.Controls),
            ["autoplay"] = JsonSerializer.SerializeToElement(block.Autoplay)
        }
    };

    public static AudioBlock FromNode(NeoPageNode node) => new()
    {
        Src = node.Properties.TryGetValue("src", out var v) ? v.GetString() ?? string.Empty : string.Empty,
        Caption = node.Properties.TryGetValue("caption", out var c) ? c.GetString() : null,
        Controls = node.Properties.TryGetValue("controls", out var ct) ? ct.ValueKind != JsonValueKind.False : true,
        Autoplay = node.Properties.TryGetValue("autoplay", out var a) && a.ValueKind == JsonValueKind.True
    };
}
