using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Hero01;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Media;

namespace Aero.Cms.Ui.Neo.Definitions;

public sealed class VideoEditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "media.video";
    public string DisplayName => "Video";
    public string? Description => "Embed a video with playback options.";
    public string Category => "Media";
    public string Kind => "Block";
    public string IconName => "video";
    public int SortOrder => 40;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(VideoBlockEditorPreview);
    public Type? PropertyEditorComponentType => typeof(VideoBlockEditor);

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return VideoBlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static VideoBlock ToBlock(EditorBlock editor) => new()
    {
        Src = editor.Src,
        Autoplay = editor.AutoPlay,
        Controls = true
    };
}
