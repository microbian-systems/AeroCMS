using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed class LegacyImageBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "image";
    public string DisplayName => "Image";
    public string? Description => null;
    public string Category => "Media";
    public string Kind => "Block";
    public string IconName => "image";
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
        return ImageBlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static Aero.Cms.Abstractions.Blocks.Neo.ImageBlock ToBlock(EditorBlock editor) => new()
    {
        Src = FirstNonEmpty(editor.Src, "https://images.unsplash.com/photo-1556761175-5973dc0f32e7?w=800"),
        Alt = editor.Alt,
        Caption = editor.Caption
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
