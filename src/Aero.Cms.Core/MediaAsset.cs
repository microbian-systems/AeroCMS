using Aero.Core.Entities;

namespace Aero.Cms.Core;

/// <summary>
/// Represents a media asset (image, video, etc.) in the CMS.
/// </summary>
public class MediaAsset : Entity
{
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? AltText { get; set; }
    public string? Description { get; set; }
}
