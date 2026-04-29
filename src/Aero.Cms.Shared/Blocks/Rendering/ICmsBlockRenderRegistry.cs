namespace Aero.Cms.Shared.Blocks.Rendering;

/// <summary>
/// Resolves compiled block render adapters by persisted block discriminator.
/// </summary>
public interface ICmsBlockRenderRegistry
{
    /// <summary>
    /// Attempts to resolve a render adapter for a persisted block type.
    /// </summary>
    /// <param name="blockType">The persisted block discriminator.</param>
    /// <param name="adapter">The resolved adapter when one exists.</param>
    /// <returns><see langword="true" /> when an adapter was resolved.</returns>
    bool TryGet(string blockType, out ICmsBlockRenderAdapter adapter);
}
