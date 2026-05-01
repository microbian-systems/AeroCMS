namespace Aero.Cms.Abstractions.Models;

[GenerateSerializer]
public record CategoryViewModel : AeroEntityViewModel
{
    [Id(0)]
    public string? Name { get; set; }
    [Id(1)]
    public string? Slug { get; set; }
    [Id(2)]
    public string? Description { get; set; }
    [Id(3)]
    public long? ParentCategoryId { get; set; }
}

[GenerateSerializer]
[Alias("CategoryErrorViewModel")]
public record CategoryErrorViewModel : AeroErrorViewModel<CategoryViewModel>;