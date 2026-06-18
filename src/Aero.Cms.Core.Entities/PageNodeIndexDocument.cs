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
    public long SiteId { get; set; }
    public long PageId { get; set; }
    public long CompositionId { get; set; }
    public string Culture { get; set; } = SitesModel.DefaultCultureName;
    public string NodeId { get; set; } = string.Empty;
    public string CatalogId { get; set; } = string.Empty;
    public NeoPageNodeKind Kind { get; set; }
    public string Path { get; set; } = string.Empty;
    public int Depth { get; set; }
}
