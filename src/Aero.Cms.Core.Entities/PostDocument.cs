using System.Text.Json.Serialization;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;

using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Stores a site- and culture-specific blog post with publication, taxonomy, and author references.
/// </summary>
public sealed class PostDocument : SableDocument, IAuditable, ISiteOwned
{
        /// <summary>
    /// Gets or sets the site identifier recorded with this post; isolation is not enforced by the entity.
    /// </summary>
public long SiteId { get; set; }
        /// <summary>
    /// Gets or sets an optional identifier grouping culture variants.
    /// </summary>
public long? TranslationGroupId { get; set; }
        /// <summary>
    /// Gets or sets an optional source-post identifier; no relationship is enforced here.
    /// </summary>
public long? SourcePostId { get; set; }
        /// <summary>
    /// Gets or sets an optional series identifier; no relationship is enforced here.
    /// </summary>
public long? SeriesId { get; set; }
        /// <summary>
    /// Gets or sets the stored culture label, defaulting to the CMS default culture without normalization.
    /// </summary>
public string Culture { get; set; } = SitesModel.DefaultCultureName;
        /// <summary>
    /// Gets or sets the stored route slug; validation and uniqueness are external concerns.
    /// </summary>
public string Slug { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the required-initialized post title.
    /// </summary>
public string Title { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets optional excerpt text.
    /// </summary>
public string? Excerpt { get; set; }
        /// <summary>
    /// Gets or sets optional SEO title metadata.
    /// </summary>
public string? SeoTitle { get; set; }
        /// <summary>
    /// Gets or sets optional SEO description metadata.
    /// </summary>
public string? SeoDescription { get; set; }

    /// <summary>
    /// Gets or sets whether the published post is eligible for the site's search index.
    /// </summary>
    public bool IncludeInSearch { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the published post may be used to ground public AI answers.
    /// </summary>
    public bool IncludeInPublicAi { get; set; }

        /// <summary>
    /// Gets or sets an optional publication timestamp; its offset is not normalized by this type.
    /// </summary>
public DateTimeOffset? PublishedOn { get; set; } = null;
        /// <summary>
    /// Gets or sets the stored publication state; changing it has no side effects here.
    /// </summary>
public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;

    /// <summary>
    /// Gets or sets the Markdown body for this blog post.
    /// </summary>
    public string MarkdownContent { get; set; } = string.Empty;

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
    /// Gets whether the stored publication state is <c>Published</c>; no other visibility rules are evaluated.
    /// </summary>
    [JsonIgnore]
    public bool IsPubliclyVisible => PublicationState == ContentPublicationState.Published;

    // IAuditable
    /// <summary>Gets or sets the creation timestamp. The default is UTC, but setters do not enforce an offset.</summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Gets or sets the last-modified timestamp; callers and persistence conventionally use UTC, but setters do not enforce it.</summary>
    public DateTimeOffset? ModifiedOn { get; set; }
    /// <summary>Gets or sets the actor recorded as creating this document, when available.</summary>
    public string? CreatedBy { get; set; }
    /// <summary>Gets or sets the actor recorded as last modifying this document, when available.</summary>
    public string? ModifiedBy { get; set; }
}
