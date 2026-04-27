using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks;

/// <summary>
/// Defines the contract for a CMS block that can be visited and rendered.
/// </summary>
public interface IBlock
{
    /// <summary>
    /// Gets the unique identifier of the block.
    /// </summary>
    long Id { get; }

    /// <summary>
    /// Gets the type discriminator of the block.
    /// </summary>
    string BlockType { get; }

    /// <summary>
    /// Gets the display order of the block within its parent content.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Accepts a visitor for rendering the block.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <returns>The HTML content rendered by the visitor.</returns>
    IHtmlContent Accept(IBlockVisitor visitor);
}
