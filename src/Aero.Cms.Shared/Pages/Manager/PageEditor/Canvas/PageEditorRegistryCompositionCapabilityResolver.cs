using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Canvas;

/// <summary>
/// Resolves composition capabilities from the page-editor definition registry.
/// This keeps drag/drop validation tied to the same catalog metadata used by
/// the palette, property panel, preview renderer, and public renderer.
/// </summary>
public sealed class PageEditorRegistryCompositionCapabilityResolver(
    IPageEditorDefinitionRegistry registry)
    : ICompositionCapabilityResolver
{
        /// <summary>
    /// TryGet method.
    /// </summary>
public bool TryGet(
        string catalogId,
        out ICompositionCapabilities capabilities)
    {
        if (registry.TryGetDescriptor(catalogId, out var descriptor))
        {
            capabilities = descriptor.Catalog.Composition;
            return true;
        }

        capabilities = default!;
        return false;
    }
}
