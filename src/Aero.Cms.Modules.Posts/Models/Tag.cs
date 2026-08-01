using Aero.Core.Data;
using Aero.Cms.Abstractions.Interfaces;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Posts.Models;

/// <summary>
/// Represents a site-owned tag that can be assigned to blog posts.
/// </summary>
public class Tag : SableDocument, IAuditable, ISiteOwned
{
    /// <summary>
    /// Gets or sets the site that owns the tag.
    /// </summary>
public long SiteId { get; set; }

    /// <summary>
    /// Gets or sets the default-culture display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default-culture route slug.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    // IAuditable
    /// <summary>
    /// Gets or sets when the tag was created.
    /// </summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets when the tag was last modified.
    /// </summary>
    public DateTimeOffset? ModifiedOn { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the principal that created the tag.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the principal that last modified the tag.
    /// </summary>
    public string? ModifiedBy { get; set; }
}
