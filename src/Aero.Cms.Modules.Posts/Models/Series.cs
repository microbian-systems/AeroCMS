using Aero.Core.Data;
using Aero.Cms.Abstractions.Interfaces;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Posts.Models;

/// <summary>
/// Represents a site-owned editorial series used to group blog posts.
/// </summary>
public sealed class Series : SableDocument, IAuditable, ISiteOwned
{
    /// <summary>
    /// Gets or sets the site that owns the series.
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

    /// <summary>
    /// Gets or sets the optional default-culture description.
    /// </summary>
    public string? Description { get; set; }

    // IAuditable
    /// <summary>
    /// Gets or sets when the series was created.
    /// </summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets when the series was last modified.
    /// </summary>
    public DateTimeOffset? ModifiedOn { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the principal that created the series.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the principal that last modified the series.
    /// </summary>
    public string? ModifiedBy { get; set; }
}
