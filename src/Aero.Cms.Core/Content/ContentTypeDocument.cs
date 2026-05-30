using Aero.Cms.Abstractions.Content;
using Aero.Core.Entities;

namespace Aero.Cms.Core.Content;

public sealed class ContentTypeDocument : Entity
{
    public long SiteId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Icon { get; set; }
    public bool AllowPublicUrl { get; set; }
    public bool HideFromSearch { get; set; }
    public List<ContentFieldDefinition> Fields { get; set; } = [];
    public string? ScribanTemplate { get; set; }
    public ContentTypeRenderMode RenderMode { get; set; }
}
