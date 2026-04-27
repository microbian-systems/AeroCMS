using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Enums;

using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

public sealed class BlogPostDocument : Entity
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Excerpt { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public DateTimeOffset? PublishedOn { get; set; } = null;
    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;

    /// <summary>
    /// Gets or sets the block-based content for this blog post.
    /// </summary>
    public List<BlockBase> Content { get; set; } = [];

    /// <summary>
    /// Gets or sets the IDs of tags associated with this blog post.
    /// </summary>
    public List<long> TagIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the IDs of categories associated with this blog post.
    /// </summary>
    public List<long> CategoryIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the ID of the author for this blog post.
    /// </summary>
    public long? AuthorId { get; set; }

    /// <summary>
    /// Gets or sets the URL of the featured image for this blog post.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the number of likes for this blog post.
    /// </summary>
    public int Likes { get; set; }

    public bool IsPubliclyVisible => PublicationState == ContentPublicationState.Published;
}
