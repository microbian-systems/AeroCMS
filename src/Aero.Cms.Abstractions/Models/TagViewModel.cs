namespace Aero.Cms.Abstractions.Models;

[Alias("TagViewModel")]
[GenerateSerializer]
public record TagViewModel : AeroEntityViewModel
{
    [Id(0)]
    public string? Name { get; set; }
    [Id(1)]
    public string? Description { get; set; }
}

[GenerateSerializer]
[Alias("TagErrorViewModel")]
public record TagErrorViewModel : AeroErrorViewModel<TagViewModel>;