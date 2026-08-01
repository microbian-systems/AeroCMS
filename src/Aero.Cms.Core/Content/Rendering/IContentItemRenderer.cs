using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Rendering;

/// <summary>
/// Renders a content item using its content type definition.
/// </summary>
public interface IContentItemRenderer
{
    /// <summary>Asynchronously renders a content item.</summary>
    /// <param name="typeDefinition">The definition that supplies the rendering template and field metadata.</param>
    /// <param name="item">The item to render.</param>
    /// <param name="ct">A token that can cancel rendering.</param>
    /// <returns>The rendered output on success; otherwise an <see cref="AeroError"/> describing the failure.</returns>
    Task<Result<string, AeroError>> RenderAsync(
        ContentTypeDefinition typeDefinition,
        ContentItem item,
        CancellationToken ct = default);
}
