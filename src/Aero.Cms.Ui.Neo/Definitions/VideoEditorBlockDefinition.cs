using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Hero01;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Media;

namespace Aero.Cms.Ui.Neo.Definitions;

/// <summary>
/// Represents a class for VideoEditorBlockDefinition.
/// </summary>
public sealed class VideoEditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "media.video";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Video";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Embed a video with playback options.";
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category => "Media";
        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public string Kind => "Block";
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public string IconName => "video";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 40;
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(VideoBlockEditorPreview);
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(VideoBlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId
    };

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return VideoBlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static VideoBlock ToBlock(EditorBlock editor) => new()
    {
        Src = editor.Src,
        Autoplay = editor.AutoPlay,
        Controls = true
    };
}
