namespace Aero.Cms.Core.Entities;

/// <summary>
/// Separates block <em>placement metadata</em> from block <em>content</em>.
/// An <c>EditorBlockPlacement</c> says "block X goes in region Y at order Z".
/// Block content lives in <see cref="Aero.Cms.Abstractions.Blocks.BlockBase"/>.
/// </summary>
public sealed class EditorBlockPlacement
{
    /// <summary>
    /// Stable client-side identifier assigned by the editor UI.
    /// Stable editor-side key for this placement.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Persisted <c>BlockBase.Id</c>. <c>null</c> for new blocks not yet saved.
    /// </summary>
    public long? BlockId { get; set; }

    /// <summary>
    /// The layout region this block belongs to (e.g. "main", "sidebar").
    /// </summary>
    public string Region { get; set; } = "main";

    /// <summary>
    /// Display order within the region. Lower = first.
    /// </summary>
    public int Order { get; set; }
}
