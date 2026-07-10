namespace Aero.Cms.Abstractions.Blocks.Neo;

/// <summary>
/// Represents a class for SeparatorBlockMapper.
/// </summary>
public static class SeparatorBlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(SeparatorBlock block) => new()
    {
        CatalogId = "ui.separator", Kind = NeoPageNodeKind.Block
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static SeparatorBlock FromNode(NeoPageNode node) => new();
}
