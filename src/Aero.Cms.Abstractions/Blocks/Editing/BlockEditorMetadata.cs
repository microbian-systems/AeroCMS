 namespace Aero.Cms.Abstractions.Blocks.Editing;

/// <summary>
/// Provides metadata about a block type for the admin UI editor.
/// </summary>
public sealed class BlockEditorMetadata
{
    /// <summary>
    /// Gets the unique name of the block type.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display name shown in the admin UI.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the description of the block type.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the category for grouping blocks in the admin UI.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the icon identifier for the block type.
    /// </summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>
    /// Gets the sort order for the block type in UI listings.
    /// </summary>
    public int SortOrder { get; init; }

    /// <summary>
    /// Gets the list of property metadata for the block's editable properties.
    /// </summary>
    public List<BlockPropertyMetadata> Properties { get; init; } = [];
}
