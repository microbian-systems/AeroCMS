using Aero.Core.Entities;

namespace Aero.Cms.Abstractions.Models;

[Alias("SiteViewModel")]
[GenerateSerializer]
public record SiteViewModel : AeroEntityViewModel
{
    [Id(1)]
    public string? Name { get; set; }
    [Id(2)]
    public string? PrimaryHost { get; set; }
    [Id(3)]
    public List<string> Hosts { get; set; } = [];
    [Id(4)]
    public bool IsEnabled { get; set; } = true;
    [Id(5)]
    public string? DefaultCulture { get; set; }
    [Id(6)]
    public long TenantId { get; set; }
}

[GenerateSerializer]
[Alias("SiteErrorViewModel")]
public record SiteErrorViewModel : AeroErrorViewModel<SiteViewModel>;