using Aero.Cms.Core;
using Aero.Cms.Modules.Pages;

namespace Aero.Cms.Modules.Blog.Requests;

public sealed record CreateBlogPostRequest
{
    public required string Title { get; init; }
    public required string Slug { get; init; }
    public string? Summary { get; init; }
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
    public List<string>? Tags { get; init; }
    public string? Category { get; init; }
    public string? Author { get; init; }
    public string? ImageUrl { get; init; }
    public ContentPublicationState PublicationState { get; init; } = ContentPublicationState.Draft;
}