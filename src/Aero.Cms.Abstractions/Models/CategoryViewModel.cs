namespace Aero.Cms.Abstractions.Models;

public record CategoryViewModel : EntityViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long? ParentCategoryId { get; set; }
}
