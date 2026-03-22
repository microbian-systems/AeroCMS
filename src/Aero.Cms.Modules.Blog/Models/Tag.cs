using Aero.Core.Entities;

namespace Aero.Cms.Modules.Blog.Models;

/// <summary>
/// Represents a tag that can be applied to blog posts for categorization.
/// </summary>
public class Tag : Entity
{
    /// <summary>
    /// Gets or sets the name of the tag.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL-friendly slug for this tag.
    /// </summary>
    public string Slug { get; set; } = string.Empty;
}
