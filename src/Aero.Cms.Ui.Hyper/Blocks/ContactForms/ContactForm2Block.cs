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
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.contact-forms.2";

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
    /// Gets or sets the Subject Label.
    /// </summary>
public string SubjectLabel { get; set; } = "Subject";
        /// <summary>
    /// Gets or sets the Subject Default Option.
    /// </summary>
public string SubjectDefaultOption { get; set; } = "Select a subject";
        /// <summary>
    /// Gets or sets the Priority Label.
    /// </summary>
public string PriorityLabel { get; set; } = "Priority";
        /// <summary>
    /// Gets or sets the Priority Default Option.
    /// </summary>
public string PriorityDefaultOption { get; set; } = "Select a priority";
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
