namespace Aero.Cms.Shared.Blocks.Rendering;

/// <summary>
/// Describes a compiled CMS block model and its renderer.
/// </summary>
public sealed record CmsBlockDescriptor(
    string BlockType,
    string DisplayName,
    string? Description,
    string? Category,
    string? Icon,
    int SortOrder,
    Type ModelType,
    Type RendererType,
    string RendererParameterName);
