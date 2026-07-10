using System.Text.Json;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Dynamic;

namespace Aero.Cms.Ui.Neo.Definitions;

/// <summary>
/// Represents a class for ScribanEditorBlockDefinition.
/// </summary>
public sealed class ScribanEditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "neo.template.scriban";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Scriban Template";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Render dynamic content using Scriban templates.";
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category => "Dynamic";
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
public int SortOrder => 100;
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => null;
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(ScribanBlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId,
        ScribanTemplate = "{{ title }}",
        ScribanDataJson = "{ \"title\": \"Hello\" }"
    };

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return ScribanBlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static ScribanBlock ToBlock(EditorBlock editor) => new()
    {
        Name = "Scriban Block",
        Template = editor.ScribanTemplate,
        Data = !string.IsNullOrWhiteSpace(editor.ScribanDataJson) && editor.ScribanDataJson != "{}"
            ? JsonDocument.Parse(editor.ScribanDataJson)
            : null
    };
}
