using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// HyperUI Footer 11 — simple footer with logo and copyright.
/// Source: hyperui/public/examples/marketing/footers/11.html.
/// </summary>
[BlockMetadata(
    "hyper.footers.11",
    "Footer 11",
    Category = "Hyper",
    Icon = "panel-bottom",
    SortOrder = 50,
    SchemaVersion = 1)]
public sealed class Footer11Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.footers.11";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Copyright.
    /// </summary>
public string Copyright { get; set; } = "Copyright &copy; 2022. All rights reserved.";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
