using Aero.Cms.Abstractions.Blocks.Editor;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

/// <summary>
/// Provider for canned / built-in / legacy block definitions.
/// Partial class — Aero UX, legacy, and alias definitions are registered
/// in separate partial files via static partial methods.
/// </summary>
public sealed partial class CannedBlockDefinitionProvider : IPageEditorBlockProvider
{
    private static readonly List<IPageEditorBlockDefinition> _definitions = [];

    static CannedBlockDefinitionProvider()
    {
        AddAeroUxDefinitions(_definitions);
        AddLegacyDefinitions(_definitions);
        AddAliasDefinitions(_definitions);
    }

    /// <summary>Implemented in CannedBlockDefinitionProvider.AeroUx.cs</summary>
    static partial void AddAeroUxDefinitions(List<IPageEditorBlockDefinition> definitions);

    /// <summary>Implemented in CannedBlockDefinitionProvider.Legacy.cs</summary>
    static partial void AddLegacyDefinitions(List<IPageEditorBlockDefinition> definitions);

    /// <summary>Implemented in CannedBlockDefinitionProvider.Aliases.cs</summary>
    static partial void AddAliasDefinitions(List<IPageEditorBlockDefinition> definitions);

        /// <summary>
    /// GetDefinitions method.
    /// </summary>
public IReadOnlyCollection<IPageEditorBlockDefinition> GetDefinitions() =>
        _definitions.AsReadOnly();
}
