using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed class LegacyRawHtmlBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "raw_html";
    public string DisplayName => "Raw HTML";
    public string? Description => null;
    public string Category => "Legacy UI";
    public string Kind => "Block";
    public string IconName => "code";
    public int SortOrder => 0;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => null;
    public Type? PropertyEditorComponentType => null;

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId,
        Content = "<div>Raw HTML</div>"
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return NeoRawHtmlBlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static NeoRawHtmlBlock ToBlock(EditorBlock editor) => new()
    {
        Html = editor.Content
    };
}
