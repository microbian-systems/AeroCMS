namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Represents a record for MediaViewModel.
/// </summary>
[Alias("Media")]
[GenerateSerializer]
public record MediaViewModel : AeroEntityViewModel
{
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
[Id(0)]
    public string? Title { get; set; }
        /// <summary>
    /// Gets or sets the Url.
    /// </summary>
[Id(1)]
    public string? Url { get; set; }
        /// <summary>
    /// Gets or sets the Thumbnail Url.
    /// </summary>
[Id(2)]
    public string? ThumbnailUrl { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
[Id(3)]
    public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
[Id(4)]
    public IList<string> Tags { get; set; } = [];
        /// <summary>
    /// Gets or sets the File Name.
    /// </summary>
[Id(5)]
    public string? FileName { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Mime Type.
    /// </summary>
[Id(6)]
    public object? MimeType { get; set; } = null;
        /// <summary>
    /// Gets or sets the File Size In Bytes.
    /// </summary>
[Id(7)]
    public long FileSizeInBytes { get; set; }
        /// <summary>
    /// Gets or sets the Dimensions.
    /// </summary>
[Id(8)]
    public (int Width, int Height) Dimensions { get; set; }
        /// <summary>
    /// Gets or sets the Alt Text.
    /// </summary>
[Id(9)]
    public string? AltText { get; set; }
        /// <summary>
    /// Gets or sets the Is Folder.
    /// </summary>
[Id(10)]
    public bool IsFolder { get; set; }
        /// <summary>
    /// Gets or sets the Parent Id.
    /// </summary>
[Id(11)]
    public long? ParentId { get; set; }
        /// <summary>
    /// Gets or sets the Content.
    /// </summary>
[Id(12)]
    public byte[]? Content { get; set; }
}

/// <summary>
/// Represents a record for MediaErrorViewModel.
/// </summary>
[GenerateSerializer]
[Alias("MediaErrorViewModel")]
public record MediaErrorViewModel : AeroErrorViewModel<MediaViewModel>;