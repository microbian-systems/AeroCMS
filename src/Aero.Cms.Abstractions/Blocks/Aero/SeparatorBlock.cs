using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

/// <summary>
/// Represents a class for SeparatorBlock.
/// </summary>
[BlockMetadata("ui.separator", "Separator", Category = "UI", Icon = "minus", SortOrder = 80, SchemaVersion = 1)]
public sealed class SeparatorBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "ui.separator";
        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
