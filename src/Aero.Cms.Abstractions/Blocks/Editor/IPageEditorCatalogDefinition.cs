using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Catalog metadata and editor behavior shared by blocks, primitives, and components.
/// </summary>
public interface IPageEditorCatalogDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
string CatalogId { get; }

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
string DisplayName { get; }

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
string? Description { get; }

        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
string Category { get; }

        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
NeoPageNodeKind Kind { get; }

        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
string IconName { get; }

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
int SortOrder { get; }

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
bool PublicStaticSsrSafe { get; }

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
Type? PreviewComponentType { get; }

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
Type? PropertyEditorComponentType { get; }

        /// <summary>
    /// Gets or sets the Composition.
    /// </summary>
ICompositionCapabilities Composition { get; }

        /// <summary>
    /// Gets or sets the Editor Capabilities.
    /// </summary>
EditorCapabilitySet EditorCapabilities { get; }
}
