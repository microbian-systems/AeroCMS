namespace Aero.Cms.Abstractions.Models;


[Alias("TenantViewModel")]
[GenerateSerializer]
public record TenantViewModel : AeroEntityViewModel
{
    [Id(0)]
    public long AccountId { get; set; }
    [Id(1)]
    public string? Name { get; set; }
    [Id(2)]
    public string? Host { get; set; }
    [Id(3)]
    public List<(long siteId, string siteName)> Settings { get; } = [];
}


[GenerateSerializer]
[Alias("TenantErrorViewModel")]
public record TenantErrorViewModel : AeroErrorViewModel<TenantViewModel>;