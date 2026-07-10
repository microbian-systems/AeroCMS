namespace Aero.Cms.Abstractions.Blocks.Neo;

/// <summary>
/// Represents a class for VideoBlockMapper.
/// </summary>
public static class VideoBlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(VideoBlock block) => new()
    {
        CatalogId = "media.video", Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["src"] = JsonSerializer.SerializeToElement(block.Src),
            ["poster"] = JsonSerializer.SerializeToElement(block.Poster ?? string.Empty),
            ["caption"] = JsonSerializer.SerializeToElement(block.Caption ?? string.Empty),
            ["autoplay"] = JsonSerializer.SerializeToElement(block.Autoplay),
            ["loop"] = JsonSerializer.SerializeToElement(block.Loop),
            ["controls"] = JsonSerializer.SerializeToElement(block.Controls)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static VideoBlock FromNode(NeoPageNode node) => new()
    {
        Src = GetS(node, "src", string.Empty),
        Poster = GetS(node, "poster", null),
        Caption = GetS(node, "caption", null),
        Autoplay = GetB(node, "autoplay", false),
        Loop = GetB(node, "loop", false),
        Controls = GetB(node, "controls", true)
    };

    private static string GetS(NeoPageNode n, string k, string d) => n.Properties.TryGetValue(k, out var v) ? v.GetString() ?? d : d;
    private static bool GetB(NeoPageNode n, string k, bool d) => n.Properties.TryGetValue(k, out var v) && v.ValueKind == JsonValueKind.True;
}
