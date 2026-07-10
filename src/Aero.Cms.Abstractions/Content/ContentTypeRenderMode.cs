namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Defines an enumeration for ContentTypeRenderMode.
/// </summary>
public enum ContentTypeRenderMode
{
    /// <summary>Renders the entire content type as one DynamicTemplateBlock</summary>
    DynamicBlock,
    /// <summary>Each field maps to a BlockInstance in the page layout</summary>
    BlockLayout
}
