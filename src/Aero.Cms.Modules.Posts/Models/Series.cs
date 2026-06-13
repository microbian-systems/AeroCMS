using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Entities;

namespace Aero.Cms.Modules.Posts.Models;

/// <summary>
/// Logical grouping for blog posts that belong to the same editorial series.
/// </summary>
public sealed class Series : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
}
