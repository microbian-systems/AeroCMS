namespace Aero.Cms.Core.Blocks.Editing;

/// <summary>
/// Defines the contract for providing block metadata to the admin UI.
/// </summary>
public interface IBlockMetadataProvider
{
    /// <summary>
    /// Gets the metadata for a specific block type.
    /// </summary>
    /// <param name="blockType">The type of block to get metadata for.</param>
    /// <returns>The block editor metadata, or null if the type is not a valid block.</returns>
    BlockEditorMetadata? GetMetadata(Type blockType);

    /// <summary>
    /// Gets metadata for all registered block types.
    /// </summary>
    /// <returns>A read-only list of all block editor metadata.</returns>
    IReadOnlyList<BlockEditorMetadata> GetAllMetadata();
}
