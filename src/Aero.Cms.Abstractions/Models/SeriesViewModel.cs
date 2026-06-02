namespace Aero.Cms.Abstractions.Models;

[Alias("SeriesViewModel")]
[GenerateSerializer]
public record SeriesViewModel : AeroEntityViewModel
{
    [Id(0)] public string? Name { get; set; }
    [Id(1)] public string? Slug { get; set; }
    [Id(2)] public string? Description { get; set; }
}

[GenerateSerializer]
[Alias("SeriesErrorViewModel")]
public record SeriesErrorViewModel : AeroErrorViewModel<SeriesViewModel>;
