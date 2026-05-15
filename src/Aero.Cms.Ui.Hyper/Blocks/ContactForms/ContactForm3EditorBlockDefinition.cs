using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ContactForms;

public sealed class ContactForm3EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.contact-forms.3";

    public string DisplayName => "Contact Form 3";

    public string? Description => "Contact form card with name, email, inquiry-type checkboxes, and message.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "message-square";

    public int SortOrder => 126;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(ContactForm3BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(ContactForm3BlockEditor);

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
        return ContactForm3BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToContactBlock(editorBlock);

    private static ContactForm3Block ToContactBlock(EditorBlock editorBlock)
    {
        return new ContactForm3Block
        {
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Send Message"),
            FormAction = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
