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
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.contact-forms.3";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Name Label.
    /// </summary>
public string NameLabel { get; set; } = "Name";
        /// <summary>
    /// Gets or sets the Name Placeholder.
    /// </summary>
public string NamePlaceholder { get; set; } = "Your name";
        /// <summary>
    /// Gets or sets the Email Label.
    /// </summary>
public string EmailLabel { get; set; } = "Email";
        /// <summary>
    /// Gets or sets the Email Placeholder.
    /// </summary>
public string EmailPlaceholder { get; set; } = "Your email";
        /// <summary>
    /// Gets or sets the Inquiry Legend.
    /// </summary>
public string InquiryLegend { get; set; } = "Inquiry";
        /// <summary>
    /// Gets or sets the Message Label.
    /// </summary>
public string MessageLabel { get; set; } = "Message";
        /// <summary>
    /// Gets or sets the Message Placeholder.
    /// </summary>
public string MessagePlaceholder { get; set; } = "Your message";
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string CtaText { get; set; } = "Send Message";
        /// <summary>
    /// Gets or sets the Form Action.
    /// </summary>
public string FormAction { get; set; } = "#";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
