using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

/// <summary>
/// Represents a class for NeoRawHtmlBlock.
/// </summary>
[BlockMetadata("ui.raw-html", "Raw HTML", Category = "UI", Icon = "code", SortOrder = 70, SchemaVersion = 1)]
public sealed class NeoRawHtmlBlock : BlockBase
{
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => "ui.raw-html";
        /// <summary>
    /// Gets or sets the Html.
    /// </summary>
public string Html { get; set; } = string.Empty;

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
