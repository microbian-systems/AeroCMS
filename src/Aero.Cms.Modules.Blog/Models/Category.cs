using Aero.Core.Entities;

namespace Aero.Cms.Modules.Blog.Models;

/// <summary>
/// Represents a category for organizing blog posts in a hierarchical structure.
/// </summary>
public class Category : Entity
{
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
}
