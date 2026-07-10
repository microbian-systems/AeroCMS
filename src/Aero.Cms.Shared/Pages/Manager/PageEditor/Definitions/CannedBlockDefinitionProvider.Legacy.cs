using Aero.Cms.Abstractions.Blocks.Editor;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

/// <summary>
/// Represents a class for CannedBlockDefinitionProvider.
/// </summary>
public sealed partial class CannedBlockDefinitionProvider
{
    static partial void AddLegacyDefinitions(List<IPageEditorBlockDefinition> definitions)
    {
        // No legacy definitions remain — primitives have replaced them.
    }
}
