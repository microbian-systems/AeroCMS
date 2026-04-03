using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

public class SitesModel : Entity
{
    public long TenantId { get; set; }
    public string? Name { get; set; } 
    public string? Hostname { get; set; } 
    public bool IsEnabled { get; set; }
    public string? DefaultCulture { get; set; }
}


