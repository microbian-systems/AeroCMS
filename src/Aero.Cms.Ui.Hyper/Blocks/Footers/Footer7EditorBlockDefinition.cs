using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// Represents a class for Footer7EditorBlockDefinition.
/// </summary>
public sealed class Footer7EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.footers.7";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Footer 7";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Newsletter signup with description, social links, and link columns.";

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
public string IconName => "panel-bottom";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 46;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Footer7BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Footer7BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Want us to email you with the latest blockbuster news?",
            Description = "Newsletter footer with social links and link columns."
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFooterBlock(editorBlock);
        return Footer7BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFooterBlock(editorBlock);

    private static Footer7Block ToFooterBlock(EditorBlock editorBlock)
    {
        return new Footer7Block
        {
            NewsletterTitle = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, "Want us to email you with the latest blockbuster news?"),
            NewsletterDescription = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, ""),
            EmailPlaceholder = FirstNonEmpty(editorBlock.Description, "john@doe.com"),
            ButtonText = FirstNonEmpty(editorBlock.CtaText, "Subscribe"),
            CopyrightText = FirstNonEmpty(editorBlock.Description, "&copy; Company 2022. All rights reserved."),
            CreatedWithText = FirstNonEmpty(editorBlock.Description, "Created with Laravel and Laravel Livewire.")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
