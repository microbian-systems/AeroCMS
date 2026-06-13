using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Runtime editor contract for block-specific defaults, preview, and save mapping.
/// Implement this in the package that owns the block.
/// </summary>
public interface IPageEditorBlockDefinition
{
    string CatalogId { get; }

    string DisplayName { get; }

    string? Description { get; }

    string Category { get; }

    string Kind { get; }

    string IconName { get; }

    int SortOrder { get; }

    bool PublicStaticSsrSafe { get; }

    Type? PreviewComponentType { get; }

    Type? PropertyEditorComponentType { get; }

    EditorBlock CreateDefaultEditorBlock();

    NeoPageNode ToNeoPageNode(EditorBlock editorBlock);

    BlockBase? ToBlockBase(EditorBlock editorBlock);
}
