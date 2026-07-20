using Aero.Core.Data;
using Aero.Cms.Abstractions.Interfaces;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Posts.Models;

/// <summary>
/// Represents a site-owned category used to organize blog posts.
/// </summary>
public class Category : SableDocument, IAuditable, ISiteOwned
{
    /// <summary>
    /// Gets or sets the site that owns the category.
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

    /// <summary>
    /// Gets or sets the parent category identifier, or <see langword="null"/> for a root category.
    /// </summary>
    public long? ParentCategoryId { get; set; }

    // IAuditable
    /// <summary>
    /// Gets or sets when the category was created.
    /// </summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets when the category was last modified.
    /// </summary>
    public DateTimeOffset? ModifiedOn { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the principal that created the category.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the principal that last modified the category.
    /// </summary>
    public string? ModifiedBy { get; set; }
}
