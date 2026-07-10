using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Announcements;

/// <summary>
/// Represents a class for Announcement6EditorBlockDefinition.
/// </summary>
public sealed class Announcement6EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.announcements.6";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Announcement 6";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Floating bottom announcement card with dismiss button.";

        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category => "Hyper";

        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public string Kind => "Block";

        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public string IconName => "info";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 86;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Announcement6BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Announcement6BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Lorem, ipsum dolor",
            CtaText = "sit amet consectetur",
            CtaUrl = "#"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToAnnouncementBlock(editorBlock);
        return Announcement6BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToAnnouncementBlock(editorBlock);

    private static Announcement6Block ToAnnouncementBlock(EditorBlock editorBlock)
    {
        return new Announcement6Block
        {
            MainText = FirstNonEmpty(editorBlock.MainText, "Lorem, ipsum dolor"),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "sit amet consectetur"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
