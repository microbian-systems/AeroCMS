namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Supplies node-native editor definitions to the unified registry.
/// </summary>
public interface IPageEditorDefinitionProvider
{
    IReadOnlyCollection<PageEditorDefinitionDescriptor> GetEditorDefinitions();
}
