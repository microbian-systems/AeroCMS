namespace Aero.Cms.Shared.Blocks.Rendering;

/// <summary>
/// Marks a Razor component as the renderer for a CMS block model type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CmsBlockRendererAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CmsBlockRendererAttribute"/> class.
    /// </summary>
    /// <param name="modelType">The concrete block model type rendered by the component.</param>
    public CmsBlockRendererAttribute(Type modelType)
    {
        ModelType = modelType;
    }

    /// <summary>
    /// Gets the concrete block model type rendered by the component.
    /// </summary>
    public Type ModelType { get; }
}
