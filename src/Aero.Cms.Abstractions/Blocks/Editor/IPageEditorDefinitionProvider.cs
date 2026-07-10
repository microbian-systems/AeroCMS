namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Supplies node-native editor definitions to the unified registry.
/// </summary>
public interface IPageEditorDefinitionProvider
{
        /// <summary>
    /// GetEditorDefinitions method.
    /// </summary>
IReadOnlyCollection<PageEditorDefinitionDescriptor> GetEditorDefinitions();
}
