using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

/// <summary>
/// Represents a class for EmptyContent1EditorBlockDefinition.
/// </summary>
public sealed class EmptyContent1EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.empty-content.1";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Empty Content 1";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Nothing found message with CTA buttons and popular search links.";

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
public int SortOrder => 118;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(EmptyContent1BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(EmptyContent1BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Hmm, nothing found",
            Description = "We couldn't find what you were looking for. Try a different search term or explore our popular categories.",
            CtaText = "Browse Popular Items",
            CtaUrl = "#",
            CtaText2 = "Refine Search",
            CtaUrl2 = "#"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToEmptyContentBlock(editorBlock);
        return EmptyContent1BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToEmptyContentBlock(editorBlock);

    private static EmptyContent1Block ToEmptyContentBlock(EditorBlock editorBlock)
    {
        return new EmptyContent1Block
        {
            Title = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, editorBlock.PageTitle, "Hmm, nothing found"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "We couldn't find what you were looking for."),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Browse Popular Items"),
            CtaUrl = string.IsNullOrWhiteSpace(editorBlock.CtaUrl) ? "#" : editorBlock.CtaUrl,
            CtaText2 = FirstNonEmpty(editorBlock.CtaText2, "Refine Search"),
            CtaUrl2 = string.IsNullOrWhiteSpace(editorBlock.CtaUrl2) ? "#" : editorBlock.CtaUrl2
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
