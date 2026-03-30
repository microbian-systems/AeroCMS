namespace Aero.Cms.Abstractions.Models;

public record AuthorViewModel
{
    public long  userId { get; set; }
    public string? Name {get;set; }
    public string? AvatarUrl {get;set; }
}