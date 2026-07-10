using System.Text.Json.Serialization;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;
using Aero.Core.Entities;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks;

/// <summary>
/// Represents the base class for all CMS blocks with AOT-compatible polymorphic serialization.
/// </summary>
/// <remarks>
/// <see cref="JsonDerivedTypeAttribute"/> discriminators and <see cref="JsonPolymorphicAttribute"/>
/// are now emitted by <c>BlockRendererGenerator</c> as <c>BlockBase.Polymorphic.g.cs</c>,
/// replacing the previously hand-maintained list on this class.
/// </remarks>
public abstract partial class BlockBase : Entity, IBlock
{
    /// <summary>
    /// Gets the type discriminator of the block.
    /// </summary>
    [JsonPropertyName("blockType")]
    public abstract string BlockType { get; }

    /// <summary>
    /// Gets the display order of the block within its parent content.
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; }

        /// <summary>
    /// Gets or sets the Responsive Style.
    /// </summary>
[JsonPropertyName("responsiveStyle")]
    public ResponsiveNodeStyle ResponsiveStyle { get; set; } = new();

    /// <summary>
    /// Accepts a visitor for rendering the block.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <returns>The HTML content rendered by the visitor.</returns>
    public abstract IHtmlContent Accept(IBlockVisitor visitor);
}
