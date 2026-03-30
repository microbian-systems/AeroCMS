using Aero.Core.Entities;

namespace Aero.Cms.Abstractions.Models;

public record MediaViewModel
{
    public long Id { get; set; }
    public long SiteId { get; set; }
    public string? Title { get; set; }
    public string? Url { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Description { get; set; }
    public IList<string> Tags { get; set; } = [];
    public string? FileName { get; set; } = string.Empty;
    public object? MimeType { get; set; } = null;
    public long FileSizeInBytes { get; set; }
    public (int Width, int Height) Dimensions { get; set; }
    public string? AltText { get; set; }
    public bool IsFolder { get; set; }
    public long? ParentId { get; set; }
    public byte[]? Content { get; set; }
    public Dictionary<string, object> MetaData{ get; } = [];

}
