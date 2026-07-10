namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Supplies PageEditor block definitions from a vertical block package.
/// </summary>
public interface IPageEditorBlockProvider
{
        /// <summary>
    /// GetDefinitions method.
    /// </summary>
IReadOnlyCollection<IPageEditorBlockDefinition> GetDefinitions();
}
