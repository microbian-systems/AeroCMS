using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Runtime editor contract for block-specific defaults, preview, and save mapping.
/// Implement this in the package that owns the block.
/// </summary>
public interface IPageEditorBlockDefinition
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
string Kind { get; }

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
    /// CreateDefaultEditorBlock method.
    /// </summary>
EditorBlock CreateDefaultEditorBlock();

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
NeoPageNode ToNeoPageNode(EditorBlock editorBlock);

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
BlockBase? ToBlockBase(EditorBlock editorBlock);
}
