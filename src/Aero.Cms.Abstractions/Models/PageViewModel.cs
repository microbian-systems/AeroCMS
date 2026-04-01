using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Abstractions.Models;

public record PageViewModel : EntityViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public PageKind Kind { get; init; }
    public string? Content { get; init; }
    public string? Author { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string> Categories { get; init; } = [];
    public IReadOnlyList<object> Blocks { get; init; } = [];     
    public bool IsPublished { get; init; }
    public DateTimeOffset? PublishedOn { get; init; }
}
