namespace Aero.Cms.Abstractions.Interfaces;

/// <summary>
/// Marks an entity as owned by a specific site in the multi-site CMS.
/// All site-owned content must implement this and be filtered by SiteId.
/// </summary>
public interface ISiteOwned
{
    long SiteId { get; set; }
}
