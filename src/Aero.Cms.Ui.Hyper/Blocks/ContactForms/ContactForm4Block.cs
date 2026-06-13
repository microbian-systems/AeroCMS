using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.ContactForms;

/// <summary>
/// HyperUI Contact Form 4 — two-column grid card with name, email, phone, and message.
/// Source: hyperui/public/examples/marketing/contact-forms/4.html, 4-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.contact-forms.4",
    "Contact Form 4",
    Category = "Hyper",
    Icon = "message-square",
    SortOrder = 127,
    SchemaVersion = 1)]
public sealed class ContactForm4Block : BlockBase
{
    public const string BlockTypeId = "hyper.contact-forms.4";

    public override string BlockType => BlockTypeId;

    public string NameLabel { get; set; } = "Name";
    public string NamePlaceholder { get; set; } = "Your name";
    public string EmailLabel { get; set; } = "Email";
    public string EmailPlaceholder { get; set; } = "Your email";
    public string PhoneLabel { get; set; } = "Phone";
    public string PhonePlaceholder { get; set; } = "Your phone";
    public string MessageLabel { get; set; } = "Message";
    public string MessagePlaceholder { get; set; } = "Your message";
    public string CtaText { get; set; } = "Send Message";
    public string FormAction { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
