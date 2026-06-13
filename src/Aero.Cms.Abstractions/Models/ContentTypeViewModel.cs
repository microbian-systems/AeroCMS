using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Orleans-serializable viewmodel for content type definitions.
/// </summary>
[Alias("ContentTypeViewModel")]
[GenerateSerializer]
public sealed record ContentTypeViewModel : AeroEntityViewModel
{
    [Id(0)]
    public string Alias { get; set; } = string.Empty;
    [Id(1)]
    public string Name { get; set; } = string.Empty;
    [Id(2)]
    public string? Description { get; set; }
    [Id(3)]
    public string? Category { get; set; }
    [Id(4)]
    public string? Icon { get; set; }
    [Id(5)]
    public string FieldsJson { get; set; } = "[]";
    [Id(6)]
    public string? ScribanTemplate { get; set; }
    [Id(7)]
    public ContentTypeRenderMode RenderMode { get; set; }
    [Id(8)]
    public bool AllowPublicUrl { get; set; }
    [Id(9)]
    public bool HideFromSearch { get; set; }
    [Id(10)]
    public ContentTypeScheduleConfig? ScheduleConfig { get; set; }
}

[GenerateSerializer]
[Alias("ContentTypeErrorViewModel")]
public record ContentTypeErrorViewModel : AeroErrorViewModel<ContentTypeViewModel>;
