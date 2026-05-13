using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Modules.Pages.Migration;

/// <summary>
/// Maps legacy BlockBase documents to NeoPageNode trees for migration.
/// </summary>
public interface ILegacyBlockMapper
{
    /// <summary>
    /// Converts a legacy <see cref="BlockBase"/> document into one or more <see cref="NeoPageNode"/>s.
    /// Returns empty when the block type is not recognized or has no meaningful migration target.
    /// </summary>
    /// <param name="block">The legacy block document to convert.</param>
    /// <returns>A list of NeoPageNode(s) representing the migrated content.</returns>
    List<NeoPageNode> MapFromBlock(BlockBase block);
}
