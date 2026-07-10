using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Hero01;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.UI;

namespace Aero.Cms.Ui.Neo.Definitions;

/// <summary>
/// Represents a class for NeoRawHtmlEditorBlockDefinition.
/// </summary>
public sealed class NeoRawHtmlEditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "ui.raw-html";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Raw HTML";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Insert custom HTML markup directly.";
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
public string IconName => "code";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 70;
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(NeoRawHtmlBlockEditorPreview);
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(NeoRawHtmlBlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId,
        Content = "<p>Custom HTML</p>"
    };

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return NeoRawHtmlBlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static NeoRawHtmlBlock ToBlock(EditorBlock editor) => new()
    {
        Html = editor.Content
    };
}
