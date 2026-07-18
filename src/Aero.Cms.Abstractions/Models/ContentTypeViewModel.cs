using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Orleans-serializable viewmodel for content type definitions.
/// </summary>
[Alias("ContentTypeViewModel")]
[GenerateSerializer]
public sealed record ContentTypeViewModel : AeroEntityViewModel
{
        /// <summary>
    /// Gets or sets the Alias.
    /// </summary>
[Id(0)]
    public string Alias { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
[Id(1)]
    public string Name { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
[Id(2)]
    public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
[Id(3)]
    public string? Category { get; set; }
        /// <summary>
    /// Gets or sets the Icon.
    /// </summary>
[Id(4)]
    public string? Icon { get; set; }
        /// <summary>
    /// Gets or sets the Fields Json.
    /// </summary>
[Id(5)]
    public string FieldsJson { get; set; } = "[]";
        /// <summary>
    /// Gets or sets the Scriban Template.
    /// </summary>
[Id(6)]
    public string? ScribanTemplate { get; set; }
        /// <summary>
    /// Gets or sets the Allow Public Url.
    /// </summary>
[Id(7)]
    public bool AllowPublicUrl { get; set; }
        /// <summary>
    /// Gets or sets the Hide From Search.
    /// </summary>
[Id(8)]
    public bool HideFromSearch { get; set; }
        /// <summary>
    /// Gets or sets the Schedule Config.
    /// </summary>
[Id(9)]
    public ContentTypeScheduleConfig? ScheduleConfig { get; set; }
}

/// <summary>
/// Represents a record for ContentTypeErrorViewModel.
/// </summary>
[GenerateSerializer]
[Alias("ContentTypeErrorViewModel")]
public record ContentTypeErrorViewModel : AeroErrorViewModel<ContentTypeViewModel>;
