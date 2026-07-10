using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Hero01;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.UI;

namespace Aero.Cms.Ui.Neo.Definitions;

/// <summary>
/// Represents a class for SeparatorEditorBlockDefinition.
/// </summary>
public sealed class SeparatorEditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "ui.separator";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Separator";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "A horizontal divider line.";
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category => "UI";
        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public string Kind => "Block";
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public string IconName => "minus";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 80;
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(SeparatorBlockEditorPreview);
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(SeparatorBlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId
    };

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return SeparatorBlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static SeparatorBlock ToBlock(EditorBlock editor) => new();
}
