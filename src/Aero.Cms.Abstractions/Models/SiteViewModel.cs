namespace Aero.Cms.Abstractions.Models;

[Alias("SiteViewModel")]
[GenerateSerializer]
public record SiteViewModel : AeroEntityViewModel
{
    [Id(0)]
    public string? Name { get; set; }
    [Id(1)]
    public string? PrimaryHost { get; set; }
    [Id(2)]
    public List<string> Hosts { get; set; } = [];
    [Id(3)]
    public bool IsEnabled { get; set; } = true;
    [Id(4)]
    public string? DefaultCulture { get; set; }
}

[GenerateSerializer]
[Alias("SiteErrorViewModel")]
public record SiteErrorViewModel : AeroErrorViewModel<SiteViewModel>;