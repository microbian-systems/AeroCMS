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
    [Id(10)]
    public long SiteId { get; init; }
    [Id(11)]
    public long? ParentId { get; init; }
    [Id(12)]
    public string? Path { get; init; }
    [Id(13)]
    public int Depth { get; init; }
    [Id(14)]
    public int Order { get; init; }
    [Id(15)]
    public bool IsHidden { get; init; }
    [Id(16)]
    public bool ShowInNavMenu { get; init; } = true;
    [Id(17)]
    public string? Summary { get; init; }
    [Id(18)]
    public string? SeoTitle { get; init; }
    [Id(19)]
    public string? SeoDescription { get; init; }
    [Id(20)]
    public bool ShowHeaderNavigation { get; init; } = true;
    [Id(21)]
    public bool HideFooter { get; init; }
    [Id(22)]
    public bool ShowChatAgent { get; init; } = true;
    [Id(23)]
    public string? LayoutRegionsJson { get; init; }
    [Id(24)]
    public string Culture { get; init; } = "en-US";
    [Id(25)]
    public long? TranslationGroupId { get; init; }
    [Id(26)]
    public ContentPublicationState PublicationState { get; init; } = ContentPublicationState.Draft;
    [Id(27)]
    public string? RootNodeJson { get; init; }
}

[GenerateSerializer]
[Alias("PageErrorViewModel")]
public record PageErrorViewModel : AeroErrorViewModel<PageViewModel>;
