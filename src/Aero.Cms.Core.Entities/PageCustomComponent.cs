using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// A site-owned reusable composition template created in the page editor.
/// </summary>
public sealed class PageCustomComponent : Entity, ISiteOwned
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
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }

        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category { get; set; } = "Custom";

        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public List<string> Tags { get; set; } = [];

        /// <summary>
    /// Gets or sets the Schema Version.
    /// </summary>
public int SchemaVersion { get; set; } = 1;

        /// <summary>
    /// Gets or sets the Root.
    /// </summary>
public NeoPageNode Root { get; set; } = new();

        /// <summary>
    /// Gets or sets the Referenced Catalog Ids.
    /// </summary>
public List<string> ReferencedCatalogIds { get; set; } = [];

        /// <summary>
    /// Gets or sets the Created At.
    /// </summary>
public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
    /// Gets or sets the Updated At.
    /// </summary>
public DateTimeOffset UpdatedAt { get; set; }
}
