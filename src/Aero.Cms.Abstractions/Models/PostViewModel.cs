using Aero.Cms.Abstractions.Enums;
using Aero.Core.Entities;

namespace Aero.Cms.Abstractions.Models;

[Alias("PostViewModel")]
[GenerateSerializer]
public sealed record PostViewModel : AeroEntityViewModel
{
    [Id(0)]
    public string? Slug { get; set; }
    [Id(1)]
    public string? Title { get; set; }
    [Id(2)]
    public string? Excerpt { get; set; }
    [Id(3)]
    public string? SeoTitle { get; set; }
    [Id(4)]
    public string? SeoDescription { get; set; }
    [Id(5)]
    public DateTimeOffset? PublishedOn { get; set; } = null;
    [Id(6)]
    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;

    /// <summary>
    /// Gets or sets the block-based content for this blog post.
    /// </summary>
    [Id(7)]
    public List<object> Content { get; set; } = [];

    /// <summary>
    /// Gets or sets the IDs of tags associated with this blog post.
    /// </summary>
    [Id(8)]
    public List<long> TagIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the IDs of categories associated with this blog post.
    /// </summary>
    [Id(9)]
    public List<long> CategoryIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the ID of the author for this blog post.
    /// </summary>
    [Id(10)]
    public long? AuthorId { get; set; }

    /// <summary>
    /// Gets or sets the URL of the featured image for this blog post.
    /// </summary>
    [Id(11)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the number of likes for this blog post.
    /// </summary>
    [Id(12)]
    public int Likes { get; set; }

    public bool IsPubliclyVisible => PublicationState == ContentPublicationState.Published;
}


[GenerateSerializer]
[Alias("PostErrorViewModel")]
public record PostErrorViewModel : AeroErrorViewModel<PostViewModel>;