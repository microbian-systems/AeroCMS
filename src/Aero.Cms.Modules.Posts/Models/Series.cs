using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Entities;

namespace Aero.Cms.Modules.Posts.Models;

/// <summary>
/// Logical grouping for blog posts that belong to the same editorial series.
/// </summary>
public sealed class Series : Entity, ISiteOwned
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public string Slug { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
}
