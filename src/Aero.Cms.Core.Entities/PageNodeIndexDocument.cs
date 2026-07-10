using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Flattened node index derived from a page composition. This gives admin tools,
/// search, and component-usage analysis a queryable model instead of spelunking
/// through nested JSON trees.
/// </summary>
public sealed class PageNodeIndexDocument : Entity<string>, ISiteOwned
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Page Id.
    /// </summary>
public long PageId { get; set; }
        /// <summary>
    /// Gets or sets the Composition Id.
    /// </summary>
public long CompositionId { get; set; }
        /// <summary>
    /// Gets or sets the Culture.
    /// </summary>
public string Culture { get; set; } = SitesModel.DefaultCultureName;
        /// <summary>
    /// Gets or sets the Node Id.
    /// </summary>
public string NodeId { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public NeoPageNodeKind Kind { get; set; }
        /// <summary>
    /// Gets or sets the Path.
    /// </summary>
public string Path { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Depth.
    /// </summary>
public int Depth { get; set; }
}
