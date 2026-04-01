using Aero.Core.Entities;

namespace Aero.Cms.Modules.Sites;

public class SitesModel : Entity
{
    public string Name { get; set; } = null!;
    public string Hostname { get; set; } = null!;
    public List<string> SecondaryHosts { get; set; } = [];
    public bool IsEnabled { get; set; } = true;
    public string? DefaultCulture { get; set; }
}