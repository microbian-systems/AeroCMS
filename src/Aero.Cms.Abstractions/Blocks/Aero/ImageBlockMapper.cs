namespace Aero.Cms.Abstractions.Blocks.Neo;

/// <summary>
/// Represents a class for ImageBlockMapper.
/// </summary>
public static class ImageBlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(ImageBlock block) => new()
    {
        CatalogId = "media.image", Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["src"] = JsonSerializer.SerializeToElement(block.Src),
            ["alt"] = JsonSerializer.SerializeToElement(block.Alt),
            ["caption"] = JsonSerializer.SerializeToElement(block.Caption ?? string.Empty),
            ["imageMediaId"] = JsonSerializer.SerializeToElement(block.ImageMediaId)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static ImageBlock FromNode(NeoPageNode node) => new()
    {
        Src = GetString(node, "src", "https://images.unsplash.com/photo-1556761175-5973dc0f32e7?w=800"),
        Alt = GetString(node, "alt", string.Empty),
        Caption = GetString(node, "caption", null),
        ImageMediaId = GetLong(node, "imageMediaId", 0)
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var v) ? v.GetString() ?? fallback : fallback;
    private static long GetLong(NeoPageNode node, string key, long fallback) =>
        node.Properties.TryGetValue(key, out var v) && v.TryGetInt64(out var n) ? n : fallback;
}
