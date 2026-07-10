using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Entities;

namespace Aero.Cms.Modules.Posts.Models;

/// <summary>
/// Represents a tag that can be applied to blog posts for categorization.
/// </summary>
public class Tag : Entity, ISiteOwned
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }

    /// <summary>
    /// Gets or sets the name of the tag.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL-friendly slug for this tag.
    /// </summary>
    public string Slug { get; set; } = string.Empty;
}
