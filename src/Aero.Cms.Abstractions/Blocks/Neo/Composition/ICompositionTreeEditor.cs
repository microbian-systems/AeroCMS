namespace Aero.Cms.Abstractions.Blocks.Neo.Composition;

/// <summary>
/// Defines an interface for ICompositionTreeEditor.
/// </summary>
public interface ICompositionTreeEditor
{
        /// <summary>
    /// Drop method.
    /// </summary>
Result<IReadOnlyList<NeoPageNode>, AeroError> Drop(
        IReadOnlyList<NeoPageNode> roots,
        CompositionDropRequest request);

        /// <summary>
    /// Remove method.
    /// </summary>
Result<IReadOnlyList<NeoPageNode>, AeroError> Remove(
        IReadOnlyList<NeoPageNode> roots,
        string nodeId);
}
