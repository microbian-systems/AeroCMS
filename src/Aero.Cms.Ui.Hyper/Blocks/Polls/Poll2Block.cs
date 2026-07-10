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
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.polls.2";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Question.
    /// </summary>
public string Question { get; set; } = "Where should we go for lunch?";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem ipsum dolor sit, amet consectetur adipisicing elit.";
        /// <summary>
    /// Gets or sets the End Date.
    /// </summary>
public string EndDate { get; set; } = "October 31, 2025";
        /// <summary>
    /// Gets or sets the End Date Iso.
    /// </summary>
public string EndDateIso { get; set; } = "2025-10-31";
        /// <summary>
    /// Gets or sets the Options.
    /// </summary>
public List<Poll2Option> Options { get; set; } = DefaultOptions.Select(CloneOption).ToList();

        /// <summary>
    /// DefaultOptions.
    /// </summary>
public static readonly List<Poll2Option> DefaultOptions =
    [
        new() { Label = "Option 1", Percentage = 45 },
        new() { Label = "Option 2", Percentage = 25 },
        new() { Label = "Option 3", Percentage = 30 }
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static Poll2Option CloneOption(Poll2Option option) => new()
    {
        Label = option.Label,
        Percentage = option.Percentage
    };
}

/// <summary>
/// Represents a class for Poll2Option.
/// </summary>
public sealed class Poll2Option
{
        /// <summary>
    /// Gets or sets the Label.
    /// </summary>
public string Label { get; set; } = "";
        /// <summary>
    /// Gets or sets the Percentage.
    /// </summary>
public int Percentage { get; set; }
}
