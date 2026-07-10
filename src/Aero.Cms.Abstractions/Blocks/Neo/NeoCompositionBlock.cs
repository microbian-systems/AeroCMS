using Aero.Cms.Abstractions.Blocks.Neo;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks;

/// <summary>
/// A BlockBase that holds a tree of <see cref="NeoPageNode"/> for user-composed
/// primitive/component content. The node tree is rendered by
/// <c>NeoCompositionBlockRenderer</c> and is not converted to individual
/// typed BlockBase documents.
/// </summary>
[BlockMetadata("neo_composition", "Neo Composition", Category = "Layout", Description = "A composition container that holds a tree of Neo UI nodes.")]
public sealed class NeoCompositionBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "neo_composition";

    /// <summary>
    /// The root-level nodes of this composition tree.
    /// Each node may have its own <see cref="NeoPageNode.Children"/> for nesting.
    /// </summary>
    public List<NeoPageNode> Nodes { get; set; } = [];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
