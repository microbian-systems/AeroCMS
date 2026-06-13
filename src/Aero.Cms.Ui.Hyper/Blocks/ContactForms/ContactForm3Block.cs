using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.ContactForms;

/// <summary>
/// HyperUI Contact Form 3 — card with name, email, inquiry-type checkboxes, and message.
/// Source: hyperui/public/examples/marketing/contact-forms/3.html, 3-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.contact-forms.3",
    "Contact Form 3",
    Category = "Hyper",
    Icon = "message-square",
    SortOrder = 126,
    SchemaVersion = 1)]
public sealed class ContactForm3Block : BlockBase
{
    public const string BlockTypeId = "hyper.contact-forms.3";

    public override string BlockType => BlockTypeId;

    public string NameLabel { get; set; } = "Name";
    public string NamePlaceholder { get; set; } = "Your name";
    public string EmailLabel { get; set; } = "Email";
    public string EmailPlaceholder { get; set; } = "Your email";
    public string InquiryLegend { get; set; } = "Inquiry";
    public string MessageLabel { get; set; } = "Message";
    public string MessagePlaceholder { get; set; } = "Your message";
    public string CtaText { get; set; } = "Send Message";
    public string FormAction { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
