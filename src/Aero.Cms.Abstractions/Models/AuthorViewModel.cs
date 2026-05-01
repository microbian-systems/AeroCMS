namespace Aero.Cms.Abstractions.Models;

[Alias("AuthorViewModel")]
[GenerateSerializer]
public record AuthorViewModel : AeroEntityViewModel
{
    [Id(0)]
    public long  userId { get; set; }
    [Id(1)]
    public string? Name {get;set; }
    [Id(2)]
    public string? AvatarUrl {get;set; }
}

[GenerateSerializer]
[Alias("AuthorErrorViewModel")]
public record AuthorErrorViewModel : AeroErrorViewModel<AuthorViewModel>;