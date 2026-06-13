using Aero.Core.Entities;

namespace Aero.Cms.Abstractions.Content;

public sealed class ContentTypeDefinition : Entity
{
    public long SiteId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Icon { get; set; }

    /// <summary>
    /// When true, entries of this type can be addressed by their own public URL.
    /// Content types are embedded-first by default to avoid accidental public pages.
    /// </summary>
    public bool AllowPublicUrl { get; set; }

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
