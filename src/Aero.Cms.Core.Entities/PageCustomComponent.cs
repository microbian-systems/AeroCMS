using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// A site-owned reusable composition template created in the page editor.
/// </summary>
public sealed class PageCustomComponent : SableDocument, IAuditable, ISiteOwned
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

    // IAuditable
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
