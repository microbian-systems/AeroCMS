using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Hero01;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Media;

namespace Aero.Cms.Ui.Neo.Definitions;

public sealed class AudioEditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "media.audio";
    public string DisplayName => "Audio";
    public string? Description => "Embed an audio player with controls.";
    public string Category => "Media";
    public string Kind => "Block";
    public string IconName => "volume-2";
    public int SortOrder => 50;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(AudioBlockEditorPreview);
    public Type? PropertyEditorComponentType => typeof(AudioBlockEditor);

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
        Controls = true
    };
}
