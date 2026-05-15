using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.ContactForms;

public sealed class ContactForm5EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.contact-forms.5";

    public string DisplayName => "Contact Form 5";

    public string? Description => "Two-column layout with contact info (phone, email, location) on the left and form on the right.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "message-square";

    public int SortOrder => 128;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(ContactForm5BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(ContactForm5BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Get in touch",
            Description = "Lorem, ipsum dolor sit amet consectetur adipisicing elit.",
            CtaText = "Send Message",
            CtaUrl = "#",
            ContactDetails =
            [
                new AeroContactDetail { Label = "Phone", Value = "+1 (555) 123-4567", Icon = "phone" },
                new AeroContactDetail { Label = "Email", Value = "info@example.com", Icon = "email" },
                new AeroContactDetail { Label = "Location", Value = "123 Main St, Anytown, USA", Icon = "location" }
            ]
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToContactBlock(editorBlock);
        return ContactForm5BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToContactBlock(editorBlock);

    private static ContactForm5Block ToContactBlock(EditorBlock editorBlock)
    {
        return new ContactForm5Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, "Get in touch"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, "Lorem, ipsum dolor sit amet consectetur adipisicing elit."),
            PhoneLabel = editorBlock.ContactDetails.Count > 0 ? FirstNonEmpty(editorBlock.ContactDetails[0].Value, "+1 (555) 123-4567") : "+1 (555) 123-4567",
            EmailLabel = editorBlock.ContactDetails.Count > 1 ? FirstNonEmpty(editorBlock.ContactDetails[1].Value, "info@example.com") : "info@example.com",
            LocationLabel = editorBlock.ContactDetails.Count > 2 ? FirstNonEmpty(editorBlock.ContactDetails[2].Value, "123 Main St, Anytown, USA") : "123 Main St, Anytown, USA",
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Send Message"),
            FormAction = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
