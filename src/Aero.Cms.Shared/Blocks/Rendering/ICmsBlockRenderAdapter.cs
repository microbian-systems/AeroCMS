using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Blocks.Rendering;

/// <summary>
/// Adapts a generic CMS block render request to a concrete Razor component renderer.
/// </summary>
public interface ICmsBlockRenderAdapter
{
    /// <summary>
    /// Gets the persisted block discriminator handled by this adapter.
    /// </summary>
    string BlockType { get; }

    /// <summary>
    /// Gets the concrete block model type handled by this adapter.
    /// </summary>
    Type ModelType { get; }

    /// <summary>
    /// Renders the block using its concrete Razor component.
    /// </summary>
    /// <param name="block">The block to render.</param>
    /// <param name="context">Cross-cutting render context.</param>
    /// <returns>A render fragment for the block.</returns>
    RenderFragment Render(IBlock block, BlockRenderContext context);
}
