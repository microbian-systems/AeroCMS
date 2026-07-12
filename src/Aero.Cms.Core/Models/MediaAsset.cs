using Aero.Core.Data;
using Aero.Cms.Abstractions.Interfaces;
using AeroDB.Sable;

namespace Aero.Cms.Core.Models;

/// <summary>
/// Represents a media asset (image, video, etc.) in the CMS.
/// </summary>
public class MediaAsset : SableDocument, IAuditable, ISiteOwned
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the File Name.
    /// </summary>
public string FileName { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Url.
    /// </summary>
public string Url { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Mime Type.
    /// </summary>
public string MimeType { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the File Size.
    /// </summary>
public long FileSize { get; set; }
        /// <summary>
    /// Gets or sets the Width.
    /// </summary>
public int Width { get; set; }
        /// <summary>
    /// Gets or sets the Height.
    /// </summary>
public int Height { get; set; }
        /// <summary>
    /// Gets or sets the Alt Text.
    /// </summary>
public string? AltText { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Is Folder.
    /// </summary>
public bool IsFolder { get; set; }
        /// <summary>
    /// Gets or sets the Parent Id.
    /// </summary>
public long? ParentId { get; set; }

    /// <summary>
    /// Optional attribution metadata for third-party sourced media
    /// (e.g. Pexels, Unsplash). Used to display credit on public pages.
    /// </summary>
    public MediaAttribution? Attribution { get; set; }

    // IAuditable
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}

/// <summary>
/// Attribution metadata for a media asset sourced from a third-party service.
/// Stored as a complete object on <see cref="MediaAsset.Attribution"/>.
/// </summary>
public sealed class MediaAttribution
{
    /// <summary>The creator/photographer name.</summary>
    public string? CreatorName { get; init; } = null;

    /// <summary>URL to the creator's profile on the source platform.</summary>
    public string? CreatorUrl { get; init; } = null;

    /// <summary>URL to the media's page on the source platform.</summary>
    public string? SourceUrl { get; init; } = null;

    /// <summary>Third-party platform name (e.g. "Pexels", "Unsplash").</summary>
    public string? Platform { get; init; } = null;

    /// <summary>Media type: "image" or "video".</summary>
    public MediaType MediaType { get; init; } = MediaType.Image;
}


/// <summary>
/// Defines an enumeration for MediaType.
/// </summary>
public enum MediaType
{
    Image,
    Video
}
