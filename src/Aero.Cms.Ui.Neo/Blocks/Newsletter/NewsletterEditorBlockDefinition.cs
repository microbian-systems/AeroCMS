using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Neo.Blocks.Newsletter;

/// <summary>
/// Represents a class for NewsletterEditorBlockDefinition.
/// </summary>
public sealed class NewsletterEditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => NewsletterBlock.BlockTypeId;

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Newsletter Signup";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "A NeoUI newsletter signup form with title, description, email input, subscribe button, and privacy notice.";

        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category => "Neo";

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
public int SortOrder => 40;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => false;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(NewsletterBlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(NewsletterBlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type              = CatalogId,
            MainText          = "Stay in the loop",
            SubText           = "Get the latest news, product updates, and tips delivered straight to your inbox.",
            CtaText           = "Subscribe",
            AlternativeLinkText = "We respect your privacy. Unsubscribe at any time.",
            SectionTitle      = "Enter your email",
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return NewsletterBlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static NewsletterBlock ToBlock(EditorBlock editor) => new()
    {
        Title       = FirstNonEmpty(editor.MainText,           "Stay in the loop"),
        Description = FirstNonEmpty(editor.SubText,            string.Empty),
        Placeholder = FirstNonEmpty(editor.SectionTitle,       "Enter your email"),
        ButtonText  = FirstNonEmpty(editor.CtaText,            "Subscribe"),
        PrivacyText = FirstNonEmpty(editor.AlternativeLinkText, "We respect your privacy. Unsubscribe at any time."),
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
