using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Converts between a persisted block and its editable Neo node representation.
/// </summary>
public interface INeoNodeBlockMapper
{
        /// <summary>
    /// ToBlock method.
    /// </summary>
BlockBase ToBlock(NeoPageNode node);

        /// <summary>
    /// ToNode method.
    /// </summary>
NeoPageNode ToNode(BlockBase block);
}
