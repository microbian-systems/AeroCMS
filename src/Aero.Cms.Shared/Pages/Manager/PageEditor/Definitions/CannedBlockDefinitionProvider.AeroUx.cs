using Aero.Cms.Abstractions.Blocks.Editor;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed partial class CannedBlockDefinitionProvider
{
    static partial void AddAeroUxDefinitions(List<IPageEditorBlockDefinition> definitions)
    {
        definitions.Add(new AeroHeroDefinition());
    }
}
