using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents a hosted site within Aero CMS.
/// Multi-domain/CNAME support is managed via the separate <see cref="SiteHost"/> entity.
/// </summary>
public class SitesModel : Entity
{
    public long TenantId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public string? DefaultCulture { get; set; }
}


