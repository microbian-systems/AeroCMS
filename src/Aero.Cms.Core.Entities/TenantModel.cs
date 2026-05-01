using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

public class TenantModel : Entity
{
    public string Name { get; set; } = default!;
    public string Hostname { get; set; } = default!;
    public string? Notes { get; set; } = null;
}