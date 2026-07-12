using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Represents a class for ContentTypeDefinition.
/// </summary>
public sealed class ContentTypeDefinition : SableDocument, IAuditable
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
    /// When true, entries of this type can be addressed by their own public URL.
    /// Content types are embedded-first by default to avoid accidental public pages.
    /// </summary>
    public bool AllowPublicUrl { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateTimeOffset? ModifiedOn { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// When true, entries of this type are not contributed to the site-wide search index.
    /// </summary>
    public bool HideFromSearch { get; set; }

    /// <summary>
    /// The fields that this content type defines.
    /// </summary>
    public List<ContentFieldDefinition> Fields { get; set; } = [];

    /// <summary>
    /// Optional custom Scriban template. When null/empty, the system
    /// auto-generates one from Fields.
    /// </summary>
    public string? ScribanTemplate { get; set; }

    /// <summary>
    /// The rendering mode: as a single dynamic block, or as individual block instances.
    /// </summary>
    public ContentTypeRenderMode RenderMode { get; set; } = ContentTypeRenderMode.DynamicBlock;

    /// <summary>
    /// Optional scheduling configuration.
    /// </summary>
    public ContentTypeScheduleConfig? ScheduleConfig { get; set; }
}
