namespace Aero.Cms.Abstractions.Blocks.Neo;

public static class SeparatorBlockMapper
{
    public static NeoPageNode ToNode(SeparatorBlock block) => new()
    {
        CatalogId = "ui.separator", Kind = NeoPageNodeKind.Block
    };

    public static SeparatorBlock FromNode(NeoPageNode node) => new();
}
