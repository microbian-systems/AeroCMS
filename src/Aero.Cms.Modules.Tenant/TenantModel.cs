using Aero.Core.Entities;

namespace Aero.Cms.Modules.Tenant;

public class TenantModel : Entity
{
    public string Name { get; set; } = default!;
    public string Hostname { get; set; } = default!;
    public string? Notes { get; set; } = null;
}
