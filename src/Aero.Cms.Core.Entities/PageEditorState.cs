namespace Aero.Cms.Core.Entities;

/// <summary>
/// The editor's draft workspace. The public renderer never reads this document.
/// Loaded by the editor API. Written on every draft save.
/// </summary>
/// <remarks>
/// V1 decision: PageEditorState remains a flat top-level block placement document.
/// It is intentionally not a page-level NeoPageNode tree. Nested composition for
/// custom Neo-authored content lives inside a <c>NeoCompositionBlock : BlockBase</c>.
/// A later PageEditor tree-view/outline may project this data visually for easier
/// navigation, but that tree-view is a UX layer, not the V1 persistence model.
/// </remarks>
public sealed class PageEditorState
{
    /// <summary>
    /// Same Id as the corresponding <see cref="PageDocument"/>.
    /// </summary>
    public long Id { get; set; }

        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }

    // ── Draft versioning ──────────────────────────────────────────────────

    /// <summary>
    /// Incremented on every draft save.
    /// Compared against <c>PageDocument.PublishedVersion</c> in the admin
    /// service layer to detect unpublished changes. Never compared inside this class.
    /// </summary>
    public long DraftVersion { get; set; }

    // ── Editor block state ────────────────────────────────────────────────

    /// <summary>
    /// The editor's working set of block placements.
    /// Each placement references a persisted <c>BlockBase</c> by <c>BlockId</c>
    /// (once saved), or carries only a <c>ClientId</c> for new blocks not yet persisted.
    /// </summary>
    public List<EditorBlockPlacement> Blocks { get; set; } = [];

    /// <summary>
    /// Maps client-side <c>EditorBlock.EditorId</c> to the persisted <c>BlockBase.Id</c>.
    /// Rebuilt on every save so existing blocks are updated in-place
    /// rather than being re-created.
    /// </summary>
    public Dictionary<string, long> BlockIdMap { get; set; } = [];

        /// <summary>
    /// Gets or sets the Last Modified.
    /// </summary>
public DateTimeOffset LastModified { get; set; }
}
