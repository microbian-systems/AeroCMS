using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed class LegacyAudioBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "audio";
    public string DisplayName => "Audio";
    public string? Description => null;
    public string Category => "Media";
    public string Kind => "Block";
    public string IconName => "volume-2";
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
        return AudioBlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static AudioBlock ToBlock(EditorBlock editor) => new()
    {
        Src = editor.Src,
        Caption = string.IsNullOrWhiteSpace(editor.Caption) ? null : editor.Caption,
        Controls = true,
        Autoplay = editor.AutoPlay
    };
}
