using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Web.Core.Blocks.Rendering;

/// <summary>
/// Defines an interface for rendering a single CMS block slice.
/// </summary>
/// <remarks>
/// Renderers are registered in <see cref="BlockSliceRegistry"/> and dispatched
/// via the visitor pattern when <see cref="BlockBase.Accept"/> is called.
/// </remarks>
public interface IBlockSliceRenderer
{
    /// <summary>
    /// Gets the block type that this renderer handles.
    /// </summary>
    Type BlockType { get; }

    /// <summary>
    /// Renders the specified block to HTML content.
    /// </summary>
    /// <param name="block">The block to render.</param>
    /// <returns>The rendered HTML content.</returns>
    IHtmlContent Render(BlockBase block);
}
