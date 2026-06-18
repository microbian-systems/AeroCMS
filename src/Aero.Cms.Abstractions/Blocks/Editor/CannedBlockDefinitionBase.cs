using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Base class for canned / built-in page-editor block definitions.
/// Each concrete class represents one block type (hero, text, pricing, etc.).
/// Kind is fixed to Block. Composition, EditorCapabilities, Interaction,
/// and CreateDefaultNode are abstract — each concrete must declare them.
/// </summary>
public abstract class CannedBlockDefinitionBase : PageEditorCatalogDefinitionBase
{
    public override NeoPageNodeKind Kind => NeoPageNodeKind.Block;
}
