using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed class LegacyColumnsBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "columns";
    public string DisplayName => "Columns";
    public string? Description => null;
    public string Category => "Legacy UI";
    public string Kind => "Block";
    public string IconName => "columns-2";
    public int SortOrder => 0;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => null;
    public Type? PropertyEditorComponentType => null;

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId,
        ColumnCount = 2,
        Gap = 16,
        EditorColumns =
        [
            new EditorColumn(),
            new EditorColumn()
        ]
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return NeoColumnsBlockMapper.ToNode(block);
    }

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
