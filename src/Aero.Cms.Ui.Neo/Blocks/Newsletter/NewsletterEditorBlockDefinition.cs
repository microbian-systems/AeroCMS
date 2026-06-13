using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Neo.Blocks.Newsletter;

public sealed class NewsletterEditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => NewsletterBlock.BlockTypeId;

    public string DisplayName => "Newsletter Signup";

    public string? Description => "A NeoUI newsletter signup form with title, description, email input, subscribe button, and privacy notice.";

    public string Category => "Neo";

    public string Kind => "Block";

    public string IconName => "mail";

    public int SortOrder => 40;

    public bool PublicStaticSsrSafe => false;

    public Type? PreviewComponentType => typeof(NewsletterBlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(NewsletterBlockEditor);

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

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return NewsletterBlockMapper.ToNode(block);
    }

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
