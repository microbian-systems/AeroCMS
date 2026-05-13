using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Builds a layout manifest from editor placements and resolved block documents.
/// Used by both the preview pipeline (transient) and the publish pipeline (persisted).
/// This is the single place where <c>EditorBlockPlacement[]</c> becomes
/// <c>LayoutRegion[]</c>.
/// </summary>
public interface IPageLayoutManifestBuilder
{
    /// <summary>
    /// Builds layout regions from the editor state and the resolved block set.
    /// Blocks must be pre-loaded by the caller; this method does not load from the store.
    /// </summary>
    /// <param name="editor">
    /// The editor state containing block placements. When <c>null</c>, returns an empty manifest.
    /// </param>
    /// <param name="blocks">Pre-loaded block documents keyed by ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<LayoutRegion>> BuildAsync(
        PageEditorState? editor,
        IReadOnlyDictionary<long, BlockBase> blocks,
        CancellationToken ct = default);
}
