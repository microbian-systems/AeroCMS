namespace Aero.Cms.Abstractions.Blocks.Rendering;

/// <summary>
/// Marks a Razor component as the renderer for a CMS block model type.
/// Vertical UI packages should use this attribute so their public renderer can
/// be discovered without adding marker code to Aero.Cms.Shared.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CmsBlockRendererAttribute : Attribute
{
    public CmsBlockRendererAttribute(Type modelType)
    {
        ModelType = modelType;
    }

    public Type ModelType { get; }
}
