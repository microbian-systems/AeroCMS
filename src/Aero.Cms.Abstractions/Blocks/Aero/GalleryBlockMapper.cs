namespace Aero.Cms.Abstractions.Blocks.Neo;

public static class GalleryBlockMapper
{
    public static NeoPageNode ToNode(GalleryBlock block) => new()
    {
        CatalogId = "media.gallery", Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["images"] = JsonSerializer.SerializeToElement(block.Images),
            ["columns"] = JsonSerializer.SerializeToElement(block.Columns)
        }
    };

    public static GalleryBlock FromNode(NeoPageNode node) => new()
    {
        Images = node.Properties.TryGetValue("images", out var v) && v.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<string>>(v.GetRawText()) ?? [] : [],
        Columns = node.Properties.TryGetValue("columns", out var c) && c.TryGetInt32(out var n) ? n : 3
    };
}
