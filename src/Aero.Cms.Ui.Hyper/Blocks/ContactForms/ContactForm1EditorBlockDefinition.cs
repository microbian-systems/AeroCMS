using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ContactForms;

public sealed class ContactForm1EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.contact-forms.1";

    public string DisplayName => "Contact Form 1";

    public string? Description => "Simple contact form card with name, email, and message fields.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "message-square";

    public int SortOrder => 124;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(ContactForm1BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(ContactForm1BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            CtaText = "Send Message",
            CtaUrl = "#"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToContactBlock(editorBlock);
        return ContactForm1BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToContactBlock(editorBlock);

    private static ContactForm1Block ToContactBlock(EditorBlock editorBlock)
    {
        return new ContactForm1Block
        {
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Send Message"),
            FormAction = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
