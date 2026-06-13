using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.ContactForms;

/// <summary>
/// HyperUI Contact Form 2 — card with name, email, subject select, priority select, and message.
/// Source: hyperui/public/examples/marketing/contact-forms/2.html, 2-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.contact-forms.2",
    "Contact Form 2",
    Category = "Hyper",
    Icon = "message-square",
    SortOrder = 125,
    SchemaVersion = 1)]
public sealed class ContactForm2Block : BlockBase
{
    public const string BlockTypeId = "hyper.contact-forms.2";

    public override string BlockType => BlockTypeId;

    public string NameLabel { get; set; } = "Name";
    public string NamePlaceholder { get; set; } = "Your name";
    public string EmailLabel { get; set; } = "Email";
    public string EmailPlaceholder { get; set; } = "Your email";
    public string SubjectLabel { get; set; } = "Subject";
    public string SubjectDefaultOption { get; set; } = "Select a subject";
    public string PriorityLabel { get; set; } = "Priority";
    public string PriorityDefaultOption { get; set; } = "Select a priority";
    public string MessageLabel { get; set; } = "Message";
    public string MessagePlaceholder { get; set; } = "Your message";
    public string CtaText { get; set; } = "Send Message";
    public string FormAction { get; set; } = "#";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
