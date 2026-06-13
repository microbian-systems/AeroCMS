using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Modules.Posts.Requests;

public sealed record UpdatePostRequest
{
    public required long Id { get; init; }
    public required string Title { get; init; }
    public required string Slug { get; init; }
    public string? Summary { get; init; }
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
    public string? MarkdownContent { get; init; }
    public List<string>? Tags { get; init; }
    public string? Category { get; init; }
    public long? SeriesId { get; init; }
    public string? Author { get; init; }
    public string? ImageUrl { get; init; }
    public ContentPublicationState PublicationState { get; init; } = ContentPublicationState.Draft;
}
