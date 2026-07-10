using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

/// <summary>
/// Represents a class for EmptyContent5EditorBlockDefinition.
/// </summary>
public sealed class EmptyContent5EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.empty-content.5";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Empty Content 5";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Out of stock message with notify and explore buttons.";

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
public string IconName => "inbox";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 122;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(EmptyContent5BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(EmptyContent5BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Out of stock",
            Description = "This item is currently unavailable. Check back soon or explore similar products.",
            CtaText = "Notify When Available",
            CtaUrl = "#",
            CtaText2 = "Explore Similar Products",
            CtaUrl2 = "#"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToEmptyContentBlock(editorBlock);
        return EmptyContent5BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToEmptyContentBlock(editorBlock);

    private static EmptyContent5Block ToEmptyContentBlock(EditorBlock editorBlock)
    {
        return new EmptyContent5Block
        {
            Title = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, editorBlock.PageTitle, "Out of stock"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "This item is currently unavailable."),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Notify When Available"),
            CtaUrl = string.IsNullOrWhiteSpace(editorBlock.CtaUrl) ? "#" : editorBlock.CtaUrl,
            CtaText2 = FirstNonEmpty(editorBlock.CtaText2, "Explore Similar Products"),
            CtaUrl2 = string.IsNullOrWhiteSpace(editorBlock.CtaUrl2) ? "#" : editorBlock.CtaUrl2,
            StatusText = "Last restocked: 3 weeks ago"
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
