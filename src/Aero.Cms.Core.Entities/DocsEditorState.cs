namespace Aero.Cms.Core.Entities;

/// <summary>
/// The editor's draft workspace for docs. The public renderer never reads this document.
/// Loaded by the docs editor API. Written on every draft save.
/// Stores the document editor's ordered block workspace.
/// </summary>
public sealed class DocsEditorState
{
    /// <summary>
    /// Same Id as the corresponding <see cref="DocsPage"/>.
    /// </summary>
    public long Id { get; set; }

        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }

    // ── Draft versioning ──────────────────────────────────────────────────

    /// <summary>
    /// Incremented on every draft save.
    /// Compared against <see cref="DocsPage.PublishedVersion"/> in the admin
    /// service layer to detect unpublished changes.
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
