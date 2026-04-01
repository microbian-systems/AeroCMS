namespace Aero.Cms.Abstractions.Models;

public record TagViewModel : EntityViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}
