using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Catalog metadata and editor behavior shared by blocks, primitives, and components.
/// </summary>
public interface IPageEditorCatalogDefinition
{
    string CatalogId { get; }

    string DisplayName { get; }

    string? Description { get; }

    string Category { get; }

    NeoPageNodeKind Kind { get; }

    string IconName { get; }

    int SortOrder { get; }

    bool PublicStaticSsrSafe { get; }

    Type? PreviewComponentType { get; }

    Type? PropertyEditorComponentType { get; }

    ICompositionCapabilities Composition { get; }

    EditorCapabilitySet EditorCapabilities { get; }
}
