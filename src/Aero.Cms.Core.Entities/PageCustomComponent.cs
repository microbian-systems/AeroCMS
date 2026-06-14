using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// A site-owned reusable composition template created in the page editor.
/// </summary>
public sealed class PageCustomComponent : Entity, ISiteOwned
{
    public long SiteId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Category { get; set; } = "Custom";

    public List<string> Tags { get; set; } = [];

    public int SchemaVersion { get; set; } = 1;

    public NeoPageNode Root { get; set; } = new();

    public List<string> ReferencedCatalogIds { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
