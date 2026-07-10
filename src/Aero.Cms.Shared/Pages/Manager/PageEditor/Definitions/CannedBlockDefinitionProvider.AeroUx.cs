using Aero.Cms.Abstractions.Blocks.Editor;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

/// <summary>
/// Represents a class for CannedBlockDefinitionProvider.
/// </summary>
public sealed partial class CannedBlockDefinitionProvider
{
    static partial void AddAeroUxDefinitions(List<IPageEditorBlockDefinition> definitions)
    {
        definitions.Add(new AeroHeroDefinition());
    }
}
