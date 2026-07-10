using Aero.Cms.Abstractions.Content;
using Aero.Core.Entities;

namespace Aero.Cms.Core.Content;

/// <summary>
/// Represents a class for ContentTypeDocument.
/// </summary>
public sealed class ContentTypeDocument : Entity
{
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Alias.
    /// </summary>
public string Alias { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string? Category { get; set; }
        /// <summary>
    /// Gets or sets the Icon.
    /// </summary>
public string? Icon { get; set; }
        /// <summary>
    /// Gets or sets the Allow Public Url.
    /// </summary>
public bool AllowPublicUrl { get; set; }
        /// <summary>
    /// Gets or sets the Hide From Search.
    /// </summary>
public bool HideFromSearch { get; set; }
        /// <summary>
    /// Gets or sets the Fields.
    /// </summary>
public List<ContentFieldDefinition> Fields { get; set; } = [];
        /// <summary>
    /// Gets or sets the Scriban Template.
    /// </summary>
public string? ScribanTemplate { get; set; }
        /// <summary>
    /// Gets or sets the Render Mode.
    /// </summary>
public ContentTypeRenderMode RenderMode { get; set; }
        /// <summary>
    /// Gets or sets the Schedule Config.
    /// </summary>
public ContentTypeScheduleConfig? ScheduleConfig { get; set; }
}
