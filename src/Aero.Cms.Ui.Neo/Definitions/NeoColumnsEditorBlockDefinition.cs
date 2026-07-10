using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Layout;

namespace Aero.Cms.Ui.Neo.Definitions;

/// <summary>
/// Represents a class for NeoColumnsEditorBlockDefinition.
/// </summary>
public sealed class NeoColumnsEditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "neo.layout.columns";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Columns";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "A multi-column layout section.";
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category => "Layout";
        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public string Kind => "Block";
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public string IconName => "columns-2";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 90;
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
public Type? PropertyEditorComponentType => typeof(NeoColumnsBlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId,
        ColumnCount = 2,
        RowCount = 1,
        Gap = 16,
        EditorColumns =
        [
            new EditorColumn(),
            new EditorColumn()
        ]
    };

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return NeoColumnsBlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static NeoColumnsBlock ToBlock(EditorBlock editor) => new()
    {
        Gap = editor.Gap > 0 ? editor.Gap : 16,
        ColumnsPerRow = editor.ColumnCount > 0 ? editor.ColumnCount : 2,
        EqualHeight = true,
        Items = editor.EditorColumns.Count > 0
            ? editor.EditorColumns
                .Select((c, i) => new ColumnItem
                {
                    Content = c.Blocks.FirstOrDefault()?.Content ?? string.Empty,
                    Span = 6
                })
                .ToList()
            : [new ColumnItem(), new ColumnItem()]
    };
}
