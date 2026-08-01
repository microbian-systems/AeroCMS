namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Represents a record for AeroSearchFilter.
/// </summary>
[Alias("AeroSearchFilter")]
[GenerateSerializer]
public sealed record AeroSearchFilter
{
        /// <summary>
    /// Gets or sets the Ids.
    /// </summary>
[Id(0)]
    public long[] Ids { get; set; } = [];
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
[Id(1)]
    public long? SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Content Type.
    /// </summary>
[Id(2)]
    public string? ContentType { get; set; }
        /// <summary>
    /// Gets or sets the Url.
    /// </summary>
[Id(3)]
    public string? Url { get; set; }
        /// <summary>
    /// Gets or sets the Is Published.
    /// </summary>
[Id(4)]
    public bool? IsPublished { get; set; }
        /// <summary>
    /// Gets or sets the Authors.
    /// </summary>
[Id(5)]
    public string[] Authors { get; set; } = [];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
[Id(6)]
    public string[] Tags { get;set; } = [];
        /// <summary>
    /// Gets or sets the Categories.
    /// </summary>
[Id(7)]
    public string[] Categories { get; set; } = [];
        /// <summary>
    /// Gets or sets the Published After.
    /// </summary>
[Id(8)]
    public DateTimeOffset? PublishedAfter { get; set; }
        /// <summary>
    /// Gets or sets the Published Before.
    /// </summary>
[Id(9)]
    public DateTimeOffset? PublishedBefore { get; set; }
        /// <summary>
    /// Gets or sets the Name Or Title.
    /// </summary>
[Id(10)]
    public string? NameOrTitle { get; set; }
        /// <summary>
    /// Gets or sets the Contains.
    /// </summary>
[Id(11)]
    public string? Contains { get; set; }
        /// <summary>
    /// Gets or sets the Page.
    /// </summary>
[Id(12)]
    public (int page, int rows) Page { get; set; }
        /// <summary>
    /// Gets or sets the Create Before.
    /// </summary>
[Id(13)]
    public DateTimeOffset? CreateBefore { get; set; }
        /// <summary>
    /// Gets or sets the Create After.
    /// </summary>
[Id(14)]
    public DateTimeOffset? CreateAfter { get; set; }
}