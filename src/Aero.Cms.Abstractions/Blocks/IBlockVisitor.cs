using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Core.Blocks;

/// <summary>
/// Defines a visitor interface for rendering CMS blocks.
/// </summary>
public interface IBlockVisitor
{
    /// <summary>
    /// Visits a CMS block and returns the rendered HTML content.
    /// </summary>
    /// <param name="block">The block to visit.</param>
    /// <returns>The rendered HTML content.</returns>
    IHtmlContent Visit(BlockBase block);
}
