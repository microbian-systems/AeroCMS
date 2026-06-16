using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Hero01;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.UI;

namespace Aero.Cms.Ui.Neo.Definitions;

public sealed class NeoRawHtmlEditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "ui.raw-html";
    public string DisplayName => "Raw HTML";
    public string? Description => "Insert custom HTML markup directly.";
    public string Category => "UI";
    public string Kind => "Block";
    public string IconName => "code";
    public int SortOrder => 70;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(NeoRawHtmlBlockEditorPreview);
    public Type? PropertyEditorComponentType => typeof(NeoRawHtmlBlockEditor);

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId,
        Content = "<p>Custom HTML</p>"
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
