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

/// <summary>
/// Typed variant of <see cref="ICmsBlockRenderAdapter"/> that provides a strongly-typed
/// Render method. Implemented by source-generated adapters alongside the untyped interface.
/// </summary>
/// <typeparam name="TBlock">The concrete block model type.</typeparam>
public interface ICmsBlockRenderAdapter<TBlock> : ICmsBlockRenderAdapter
    where TBlock : BlockBase
{
    /// <summary>
    /// Renders the block using its concrete Razor component with compile-time type safety.
    /// </summary>
    /// <param name="block">The strongly-typed block to render.</param>
    /// <param name="context">Cross-cutting render context.</param>
    /// <returns>A render fragment for the block.</returns>
    RenderFragment Render(TBlock block, BlockRenderContext context);
}
