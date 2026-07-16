namespace Aero.Cms.Abstractions.Models;

[GenerateSerializer]
[Alias("SiteStyleColorTokenViewModel")]
public sealed record SiteStyleColorTokenViewModel
{
    [Id(0)]
    public string Name { get; init; } = string.Empty;

    [Id(1)]
    public string HexValue { get; init; } = string.Empty;
}

[GenerateSerializer]
[Alias("SiteStyleProfileViewModel")]
public sealed record SiteStyleProfileViewModel
{
    [Id(0)]
    public long Revision { get; init; }

    [Id(1)]
    public decimal SmallScreenBreakpointRem { get; init; }

    [Id(2)]
    public List<SiteStyleColorTokenViewModel> ColorTokens { get; init; } = [];
}
