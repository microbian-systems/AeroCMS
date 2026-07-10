using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

/// <summary>
/// Represents a class for EmptyContent4EditorBlockDefinition.
/// </summary>
public sealed class EmptyContent4EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.empty-content.4";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Empty Content 4";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Explore more message with link cards and back to shopping CTA.";

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
public int SortOrder => 121;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(EmptyContent4BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(EmptyContent4BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Explore more",
            Description = "This section doesn't have content right now. Discover related topics and inspiration instead.",
            CtaText = "Back to Shopping",
            CtaUrl = "#",
            FeatureItems = EmptyContent4Block.DefaultLinks.Select(ToEditorLink).ToList()
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToEmptyContentBlock(editorBlock);
        return EmptyContent4BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToEmptyContentBlock(editorBlock);

    private static EmptyContent4Block ToEmptyContentBlock(EditorBlock editorBlock)
    {
        return new EmptyContent4Block
        {
            Title = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, editorBlock.PageTitle, "Explore more"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "This section doesn't have content right now."),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Back to Shopping"),
            CtaUrl = string.IsNullOrWhiteSpace(editorBlock.CtaUrl) ? "#" : editorBlock.CtaUrl,
            Links = editorBlock.FeatureItems.Count > 0
                ? editorBlock.FeatureItems.Select(ToEmptyContentLink).ToList()
                : EmptyContent4Block.DefaultLinks.Select(CloneLink).ToList()
        };
    }

    private static AeroFeatureItem ToEditorLink(EmptyContentLink l) => new()
    {
        Title = l.Title,
        Description = l.Description,
        LinkUrl = l.Url
    };

    private static EmptyContentLink ToEmptyContentLink(AeroFeatureItem f) => new()
    {
        Title = f.Title ?? string.Empty,
        Description = f.Description ?? string.Empty,
        Url = string.IsNullOrWhiteSpace(f.LinkUrl) ? "#" : f.LinkUrl!
    };

    private static EmptyContentLink CloneLink(EmptyContentLink l) => new()
    {
        Title = l.Title,
        Description = l.Description,
        Url = l.Url
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
