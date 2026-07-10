using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.NewsletterSignup;

/// <summary>
/// Represents a class for NewsletterSignup1EditorBlockDefinition.
/// </summary>
public sealed class NewsletterSignup1EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.newsletter-signup.1";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Newsletter Signup 1";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Left-aligned newsletter signup form with email input and CTA button.";

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
public string IconName => "mail";

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
public Type? PreviewComponentType => typeof(NewsletterSignup1BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(NewsletterSignup1BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Sign up for our newsletter",
            Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit.",
            CtaText = "Sign Up",
            CtaUrl = "#"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToNewsletterBlock(editorBlock);
        return NewsletterSignup1BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToNewsletterBlock(editorBlock);

    private static NewsletterSignup1Block ToNewsletterBlock(EditorBlock editorBlock)
    {
        return new NewsletterSignup1Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, "Sign up for our newsletter"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, "Lorem ipsum dolor sit amet consectetur adipisicing elit."),
            Placeholder = "Enter your email",
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Sign Up"),
            FormAction = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
