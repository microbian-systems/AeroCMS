using Aero.Core.Entities;

namespace Aero.Cms.Core.Models;

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
    public bool IsFolder { get; set; }
    public long? ParentId { get; set; }

    /// <summary>
    /// Optional attribution metadata for third-party sourced media
    /// (e.g. Pexels, Unsplash). Used to display credit on public pages.
    /// </summary>
    public MediaAttribution? Attribution { get; set; }
}

/// <summary>
/// Attribution metadata for a media asset sourced from a third-party service.
/// Stored as a complete object on <see cref="MediaAsset.Attribution"/>.
/// </summary>
public sealed record MediaAttribution
{
    /// <summary>The creator/photographer name.</summary>
    public string CreatorName { get; init; } = string.Empty;

    /// <summary>URL to the creator's profile on the source platform.</summary>
    public string CreatorUrl { get; init; } = string.Empty;

    /// <summary>URL to the media's page on the source platform.</summary>
    public string SourceUrl { get; init; } = string.Empty;

    /// <summary>Third-party platform name (e.g. "Pexels", "Unsplash").</summary>
    public string Platform { get; init; } = string.Empty;

    /// <summary>Media type: "image" or "video".</summary>
    public string MediaType { get; init; } = "image";
}
