using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Rendering;

/// <summary>
/// Defines an interface for IContentItemRenderer.
/// </summary>
public interface IContentItemRenderer
{
        /// <summary>
    /// RenderAsync method.
    /// </summary>
Task<Result<string, AeroError>> RenderAsync(
        ContentTypeDefinition typeDefinition,
        ContentItem item,
        CancellationToken ct = default);
}
