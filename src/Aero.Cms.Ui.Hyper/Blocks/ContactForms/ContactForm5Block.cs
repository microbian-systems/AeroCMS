using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.ContactForms;

/// <summary>
/// HyperUI Contact Form 5 — two-column layout with contact info (phone, email, location) and form.
/// Source: hyperui/public/examples/marketing/contact-forms/5.html, 5-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.contact-forms.5",
    "Contact Form 5",
    Category = "Hyper",
    Icon = "message-square",
    SortOrder = 128,
    SchemaVersion = 1)]
public sealed class ContactForm5Block : BlockBase
{
    public const string BlockTypeId = "hyper.contact-forms.5";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Get in touch";
    public string Description { get; set; } = "Lorem, ipsum dolor sit amet consectetur adipisicing elit. Sed voluptas delectus alias magni velit! Dicta corrupti dignissimos dolor consequatur illum tempore consectetur hic a cupiditate sunt quam, earum nisi aperiam.";
    public string PhoneLabel { get; set; } = "+1 (555) 123-4567";
    public string EmailLabel { get; set; } = "info@example.com";
    public string LocationLabel { get; set; } = "123 Main St, Anytown, USA";
    public string NameLabel { get; set; } = "Name";
    public string NamePlaceholder { get; set; } = "Your name";
    public string EmailFieldLabel { get; set; } = "Email";
    public string EmailPlaceholder { get; set; } = "Your email";
    public string MessageLabel { get; set; } = "Message";
    public string MessagePlaceholder { get; set; } = "Your message";
    public string CtaText { get; set; } = "Send Message";
    public string FormAction { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
