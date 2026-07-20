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
    /// Gets or sets the identifier of the site that owns this asset.
    /// </summary>
    public long SiteId { get; set; }
    /// <summary>
    /// Gets or sets the file name presented to CMS users.
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the URL from which the asset can be retrieved.
    /// </summary>
    public string Url { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the asset's MIME type.
    /// </summary>
    public string MimeType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the asset size in bytes.
    /// </summary>
    public long FileSize { get; set; }
    /// <summary>
    /// Gets or sets the media width when applicable.
    /// </summary>
    public int Width { get; set; }
    /// <summary>
    /// Gets or sets the media height when applicable.
    /// </summary>
    public int Height { get; set; }
    /// <summary>
    /// Gets or sets the optional alternative text for the asset.
    /// </summary>
    public string? AltText { get; set; }
    /// <summary>
    /// Gets or sets an optional description of the asset.
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// Gets or sets whether this asset represents a folder rather than a file.
    /// </summary>
    public bool IsFolder { get; set; }
    /// <summary>
    /// Gets or sets the parent folder identifier, if this asset is nested.
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    /// Optional attribution metadata for third-party sourced media
    /// (e.g. Pexels, Unsplash). Used to display credit on public pages.
    /// </summary>
    public MediaAttribution? Attribution { get; set; }

    // IAuditable
    /// <summary>Gets or sets the audit creation timestamp.</summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Gets or sets the most recent audit modification timestamp.</summary>
    public DateTimeOffset? ModifiedOn { get; set; }
    /// <summary>Gets or sets the identity that created this asset, if recorded.</summary>
    public string? CreatedBy { get; set; }
    /// <summary>Gets or sets the identity that last modified this asset, if recorded.</summary>
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
/// Identifies the kind of third-party media described by <see cref="MediaAttribution"/>.
/// </summary>
public enum MediaType
{
    /// <summary>An image asset.</summary>
    Image,
    /// <summary>A video asset.</summary>
    Video
}
