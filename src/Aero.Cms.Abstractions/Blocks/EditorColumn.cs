namespace Aero.Cms.Abstractions.Blocks;

/// <summary>Column within a Columns block in the editor.</summary>
public class EditorColumn
{
        /// <summary>
    /// Gets or sets the Col Id.
    /// </summary>
public string           ColId  { get; set; } = Guid.NewGuid().ToString();
        /// <summary>
    /// Gets or sets the Blocks.
    /// </summary>
public List<NestedBlock> Blocks { get; set; } = [];
}