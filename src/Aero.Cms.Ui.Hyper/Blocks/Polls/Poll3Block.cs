using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Polls;

/// <summary>
/// HyperUI Poll 3 — star rating poll with 1-5 stars.
/// Source: hyperui/public/examples/marketing/polls/3.html.
/// </summary>
[BlockMetadata(
    "hyper.polls.3",
    "Poll 3",
    Category = "Hyper",
    Icon = "bar-chart-2",
    SortOrder = 134,
    SchemaVersion = 1)]
public sealed class Poll3Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.polls.3";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Question.
    /// </summary>
public string Question { get; set; } = "Leave a rating";
        /// <summary>
    /// Gets or sets the Poll Name.
    /// </summary>
public string PollName { get; set; } = "Rating1";
        /// <summary>
    /// Gets or sets the Max Rating.
    /// </summary>
public int MaxRating { get; set; } = 5;

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
