using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed class LegacyCarouselBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "carousel";
    public string DisplayName => "Carousel";
    public string? Description => null;
    public string Category => "Media";
    public string Kind => "Block";
    public string IconName => "layout-grid";
    public int SortOrder => 0;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => null;
    public Type? PropertyEditorComponentType => null;

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
