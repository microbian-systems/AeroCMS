using Aero.Core.Data;
using Aero.Cms.Abstractions.Interfaces;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Posts.Models;

/// <summary>
/// Represents a category for organizing blog posts in a hierarchical structure.
/// </summary>
public class Category : SableDocument, IAuditable, ISiteOwned
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }

    /// <summary>
    /// Gets or sets the name of the category.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL-friendly slug for this category.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional description of this category.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the ID of the parent category for hierarchical organization.
    /// </summary>
    public long? ParentCategoryId { get; set; }

    // IAuditable
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
