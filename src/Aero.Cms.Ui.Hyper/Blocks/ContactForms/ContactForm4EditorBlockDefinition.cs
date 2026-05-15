using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ContactForms;

public sealed class ContactForm4EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.contact-forms.4";

    public string DisplayName => "Contact Form 4";

    public string? Description => "Two-column grid contact form card with name, email, phone, and message.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "message-square";

    public int SortOrder => 127;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(ContactForm4BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(ContactForm4BlockEditor);

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
        return ContactForm4BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToContactBlock(editorBlock);

    private static ContactForm4Block ToContactBlock(EditorBlock editorBlock)
    {
        return new ContactForm4Block
        {
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Send Message"),
            FormAction = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
