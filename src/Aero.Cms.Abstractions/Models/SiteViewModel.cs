namespace Aero.Cms.Abstractions.Models;

public record SiteViewModel : EntityViewModel
{
    public string Name { get; set; } = null!;
    public string PrimaryHost { get; set; } = null!;
    public List<string> Hosts { get; set; } = [];
    public bool IsEnabled { get; set; } = true;
    public string? DefaultCulture { get; set; }
}
