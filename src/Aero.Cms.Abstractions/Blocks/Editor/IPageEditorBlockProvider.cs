namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Supplies PageEditor block definitions from a vertical block package.
/// </summary>
public interface IPageEditorBlockProvider
{
    IReadOnlyCollection<IPageEditorBlockDefinition> GetDefinitions();
}
