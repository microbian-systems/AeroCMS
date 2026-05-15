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
    public const string BlockTypeId = "hyper.polls.3";

    public override string BlockType => BlockTypeId;

    public string Question { get; set; } = "Leave a rating";
    public string PollName { get; set; } = "Rating1";
    public int MaxRating { get; set; } = 5;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
