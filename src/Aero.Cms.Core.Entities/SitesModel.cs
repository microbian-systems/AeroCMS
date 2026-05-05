using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents a hosted site within Aero CMS.
/// Supports multiple domains/CNAMEs for site resolution.
/// </summary>
public class SitesModel : Entity
{
    public long TenantId { get; set; }
    public string? Name { get; set; }
    /// <summary>Canonical host/domain for this site.</summary>
    public string? PrimaryHost { get; set; }
    /// <summary>All domains that resolve to this site (must include PrimaryHost).</summary>
    public List<string> Hosts { get; set; } = [];
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public string? DefaultCulture { get; set; }
}


