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
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.contact-forms.5";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Get in touch";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem, ipsum dolor sit amet consectetur adipisicing elit. Sed voluptas delectus alias magni velit! Dicta corrupti dignissimos dolor consequatur illum tempore consectetur hic a cupiditate sunt quam, earum nisi aperiam.";
        /// <summary>
    /// Gets or sets the Phone Label.
    /// </summary>
public string PhoneLabel { get; set; } = "+1 (555) 123-4567";
        /// <summary>
    /// Gets or sets the Email Label.
    /// </summary>
public string EmailLabel { get; set; } = "info@example.com";
        /// <summary>
    /// Gets or sets the Location Label.
    /// </summary>
public string LocationLabel { get; set; } = "123 Main St, Anytown, USA";
        /// <summary>
    /// Gets or sets the Name Label.
    /// </summary>
public string NameLabel { get; set; } = "Name";
        /// <summary>
    /// Gets or sets the Name Placeholder.
    /// </summary>
public string NamePlaceholder { get; set; } = "Your name";
        /// <summary>
    /// Gets or sets the Email Field Label.
    /// </summary>
public string EmailFieldLabel { get; set; } = "Email";
        /// <summary>
    /// Gets or sets the Email Placeholder.
    /// </summary>
public string EmailPlaceholder { get; set; } = "Your email";
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
