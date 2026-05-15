using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.NewsletterSignup;

public sealed class NewsletterSignup2EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.newsletter-signup.2";

    public string DisplayName => "Newsletter Signup 2";

    public string? Description => "Centered newsletter signup form with email input and CTA button.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "mail";

    public int SortOrder => 123;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(NewsletterSignup2BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(NewsletterSignup2BlockEditor);

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

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToNewsletterBlock(editorBlock);
        return NewsletterSignup2BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToNewsletterBlock(editorBlock);

    private static NewsletterSignup2Block ToNewsletterBlock(EditorBlock editorBlock)
    {
        return new NewsletterSignup2Block
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
