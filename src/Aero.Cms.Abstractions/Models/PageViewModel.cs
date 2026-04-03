using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Abstractions.Models;


[Alias("PageViewModel")]
[GenerateSerializer]
public record PageViewModel : AeroEntityViewModel
{
    [Id(0)]
    public string? Title { get; init; }
    [Id(1)]
    public string? Slug { get; init; } 
    [Id(2)]
    public PageKind Kind { get; init; }
    [Id(3)]
    public string? Content { get; init; }
    [Id(4)]
    public string? Author { get; init; }
    [Id(5)]
    public IReadOnlyList<string> Tags { get; init; } = [];
    [Id(6)]
    public IReadOnlyList<string> Categories { get; init; } = [];
    [Id(7)]
    public IReadOnlyList<object> Blocks { get; init; } = [];
    [Id(8)]
    public bool IsPublished { get; init; }
    [Id(9)]
    public DateTimeOffset? PublishedOn { get; init; }
}

[GenerateSerializer]
[Alias("PageErrorViewModel")]
public record PageErrorViewModel : AeroErrorViewModel<PageViewModel>;