using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents a class for CategoryTranslation.
/// </summary>
public sealed class CategoryTranslation : Entity, ICultureAware
{
        /// <summary>
    /// Gets or sets the Category Id.
    /// </summary>
public long CategoryId { get; set; }
        /// <summary>
    /// Gets or sets the Culture.
    /// </summary>
public string Culture { get; set; } = SitesModel.DefaultCultureName;
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
