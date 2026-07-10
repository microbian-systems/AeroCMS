using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents a class for TagTranslation.
/// </summary>
public sealed class TagTranslation : Entity, ICultureAware
{
        /// <summary>
    /// Gets or sets the Tag Id.
    /// </summary>
public long TagId { get; set; }
        /// <summary>
    /// Gets or sets the Culture.
    /// </summary>
public string Culture { get; set; } = SitesModel.DefaultCultureName;
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
}
