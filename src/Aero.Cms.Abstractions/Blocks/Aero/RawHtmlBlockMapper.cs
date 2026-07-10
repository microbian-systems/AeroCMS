namespace Aero.Cms.Abstractions.Blocks.Neo;

/// <summary>
/// Represents a class for NeoRawHtmlBlockMapper.
/// </summary>
public static class NeoRawHtmlBlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(NeoRawHtmlBlock block) => new()
    {
        CatalogId = "ui.raw-html", Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["html"] = JsonSerializer.SerializeToElement(block.Html)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static NeoRawHtmlBlock FromNode(NeoPageNode node) => new()
    {
        Html = node.Properties.TryGetValue("html", out var v) ? v.GetString() ?? string.Empty : string.Empty
    };
}
