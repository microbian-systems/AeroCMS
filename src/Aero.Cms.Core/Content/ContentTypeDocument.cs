using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Core.Content;

public sealed class ContentTypeDocument
{
    public string Id { get; set; } = string.Empty;   // "{siteId}:{alias}"
    public long SiteId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Icon { get; set; }
    public List<ContentFieldDefinition> Fields { get; set; } = [];
    public string? ScribanTemplate { get; set; }
    public ContentTypeRenderMode RenderMode { get; set; }
}
