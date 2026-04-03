namespace Aero.Cms.Abstractions.Models;

[Alias("Media")]
[GenerateSerializer]
public record MediaViewModel : AeroEntityViewModel
{
    [Id(0)]
    public string? Title { get; set; }
    [Id(1)]
    public string? Url { get; set; }
    [Id(2)]
    public string? ThumbnailUrl { get; set; }
    [Id(3)]
    public string? Description { get; set; }
    [Id(4)]
    public IList<string> Tags { get; set; } = [];
    [Id(5)]
    public string? FileName { get; set; } = string.Empty;
    [Id(6)]
    public object? MimeType { get; set; } = null;
    [Id(7)]
    public long FileSizeInBytes { get; set; }
    [Id(8)]
    public (int Width, int Height) Dimensions { get; set; }
    [Id(9)]
    public string? AltText { get; set; }
    [Id(10)]
    public bool IsFolder { get; set; }
    [Id(11)]
    public long? ParentId { get; set; }
    [Id(12)]
    public byte[]? Content { get; set; }
}

[GenerateSerializer]
[Alias("MediaErrorViewModel")]
public record MediaErrorViewModel : AeroErrorViewModel<MediaViewModel>;