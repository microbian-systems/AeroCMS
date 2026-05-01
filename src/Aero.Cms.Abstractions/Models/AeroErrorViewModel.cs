namespace Aero.Cms.Abstractions.Models;

[GenerateSerializer]
[Alias("AeroErrorViewModel")]
public abstract record AeroErrorViewModel<T>
{
    [Id(2000)]
    public string? Message { get; init; }
    [Id(2001)]
    public IList<string> Errors { get; init; } = [];
    [Id(2002)]
    public T? Data { get; init; }
    public bool Success => Errors.Count == 0;
}
