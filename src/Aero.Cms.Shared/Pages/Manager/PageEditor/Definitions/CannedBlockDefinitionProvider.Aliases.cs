using Aero.Cms.Abstractions.Blocks.Editor;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed partial class CannedBlockDefinitionProvider
{
    static partial void AddAliasDefinitions(List<IPageEditorBlockDefinition> definitions)
    {
        definitions.Add(new LegacyImageBlockDefinition());
        definitions.Add(new LegacySeparatorBlockDefinition());
    }
}
