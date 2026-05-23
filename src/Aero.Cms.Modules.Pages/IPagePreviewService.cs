using Aero.Cms.Abstractions.Blocks.Layout;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Builds a transient render manifest from the current editor state
/// for draft/preview rendering. Never persists anything and never
/// touches <see cref="PageDocument.LayoutRegions"/>.
/// </summary>
public interface IPagePreviewService
{
    /// <summary>
    /// Builds a transient render manifest from the current editor (draft) state.
    /// </summary>
    /// <param name="pageId">The ID of the page to preview.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="Aero.Core.Result{T, TError}"/> containing the preview model
    /// or an <see cref="Aero.Core.AeroError"/>.
    /// </returns>
    Task<Result<PreviewRenderModel, Aero.Core.AeroError>> BuildPreviewAsync(
        long pageId,
        CancellationToken ct = default);
}

/// <summary>
/// Transient preview model used by the draft/preview rendering pipeline.
/// The renderer should use <see cref="PreviewLayout"/>, never
/// <see cref="PageDocument.LayoutRegions"/>.
/// </summary>
public sealed class PreviewRenderModel
{
    /// <summary>
    /// Page metadata (title, slug, display settings).
    /// <see cref="PageDocument.LayoutRegions"/> on this instance is ignored.
    /// </summary>
    public PageDocument PageMeta { get; init; } = null!;

    /// <summary>
    /// Transient layout built from the current draft state.
    /// </summary>
    public IReadOnlyList<LayoutRegion> PreviewLayout { get; init; } = [];

    /// <summary>
    /// Always <c>true</c> for preview models.
    /// </summary>
    public bool IsDraft => true;
}
