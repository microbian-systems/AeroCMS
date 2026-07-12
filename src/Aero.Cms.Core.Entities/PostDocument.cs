using System.Text.Json.Serialization;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;

using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents a class for PostDocument.
/// </summary>
public sealed class PostDocument : SableDocument, IAuditable, ISiteOwned
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Translation Group Id.
    /// </summary>
public long? TranslationGroupId { get; set; }
        /// <summary>
    /// Gets or sets the Source Post Id.
    /// </summary>
public long? SourcePostId { get; set; }
        /// <summary>
    /// Gets or sets the Series Id.
    /// </summary>
public long? SeriesId { get; set; }
        /// <summary>
    /// Gets or sets the Culture.
    /// </summary>
public string Culture { get; set; } = SitesModel.DefaultCultureName;
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public string Slug { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Excerpt.
    /// </summary>
public string? Excerpt { get; set; }
        /// <summary>
    /// Gets or sets the Seo Title.
    /// </summary>
public string? SeoTitle { get; set; }
        /// <summary>
    /// Gets or sets the Seo Description.
    /// </summary>
public string? SeoDescription { get; set; }
        /// <summary>
    /// Gets or sets the Published On.
    /// </summary>
public DateTimeOffset? PublishedOn { get; set; } = null;
        /// <summary>
    /// Gets or sets the Publication State.
    /// </summary>
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

        /// <summary>
    /// Gets or sets the Is Publicly Visible.
    /// </summary>
    [JsonIgnore]
    public bool IsPubliclyVisible => PublicationState == ContentPublicationState.Published;

    // IAuditable
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
