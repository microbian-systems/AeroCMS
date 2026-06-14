using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Converts between a persisted block and its editable Neo node representation.
/// </summary>
public interface INeoNodeBlockMapper
{
    BlockBase ToBlock(NeoPageNode node);

    NeoPageNode ToNode(BlockBase block);
}
