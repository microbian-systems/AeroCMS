using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Hero01;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Media;

namespace Aero.Cms.Ui.Neo.Definitions;

public sealed class GalleryEditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "media.gallery";
    public string DisplayName => "Gallery";
    public string? Description => "An image gallery with grid layout.";
    public string Category => "Media";
    public string Kind => "Block";
    public string IconName => "layout-grid";
    public int SortOrder => 60;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(GalleryBlockEditorPreview);
    public Type? PropertyEditorComponentType => typeof(GalleryBlockEditor);

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return GalleryBlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static GalleryBlock ToBlock(EditorBlock editor) => new()
    {
        Images = editor.GalleryImages.Select(g => g.Src).ToList(),
        Columns = 3
    };
}
