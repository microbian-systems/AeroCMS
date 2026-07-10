using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Hero01;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Media;

namespace Aero.Cms.Ui.Neo.Definitions;

/// <summary>
/// Represents a class for ImageEditorBlockDefinition.
/// </summary>
public sealed class ImageEditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "media.image";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Image";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "A single image with optional caption.";
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
public string IconName => "image";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 30;
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(ImageBlockEditorPreview);
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(ImageBlockEditor);

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
        return ImageBlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
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
