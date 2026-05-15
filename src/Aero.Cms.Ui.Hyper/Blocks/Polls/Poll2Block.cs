using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Polls;

/// <summary>
/// HyperUI Poll 2 — multi-choice poll with progress bars and checkboxes.
/// Source: hyperui/public/examples/marketing/polls/2.html.
/// </summary>
[BlockMetadata(
    "hyper.polls.2",
    "Poll 2",
    Category = "Hyper",
    Icon = "bar-chart-2",
    SortOrder = 133,
    SchemaVersion = 1)]
public sealed class Poll2Block : BlockBase
{
    public const string BlockTypeId = "hyper.polls.2";

    public override string BlockType => BlockTypeId;

    public string Question { get; set; } = "Where should we go for lunch?";
    public string Description { get; set; } = "Lorem ipsum dolor sit, amet consectetur adipisicing elit.";
    public string EndDate { get; set; } = "October 31, 2025";
    public string EndDateIso { get; set; } = "2025-10-31";
    public List<Poll2Option> Options { get; set; } = DefaultOptions.Select(CloneOption).ToList();

    public static readonly List<Poll2Option> DefaultOptions =
    [
        new() { Label = "Option 1", Percentage = 45 },
        new() { Label = "Option 2", Percentage = 25 },
        new() { Label = "Option 3", Percentage = 30 }
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static Poll2Option CloneOption(Poll2Option option) => new()
    {
        Label = option.Label,
        Percentage = option.Percentage
    };
}

public sealed class Poll2Option
{
    public string Label { get; set; } = "";
    public int Percentage { get; set; }
}
