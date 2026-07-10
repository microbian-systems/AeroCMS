using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.NewsletterSignup;

/// <summary>
/// HyperUI Newsletter Signup 1 — left-aligned signup form with email input and CTA button.
/// Source: hyperui/public/examples/marketing/newsletter-signup/1.html, 1-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.newsletter-signup.1",
    "Newsletter Signup 1",
    Category = "Hyper",
    Icon = "mail",
    SortOrder = 122,
    SchemaVersion = 1)]
public sealed class NewsletterSignup1Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.newsletter-signup.1";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Sign up for our newsletter";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Tenetur doloremque saepe architecto maiores repudiandae amet perferendis repellendus, reprehenderit voluptas sequi.";
        /// <summary>
    /// Gets or sets the Placeholder.
    /// </summary>
public string Placeholder { get; set; } = "Enter your email";
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string CtaText { get; set; } = "Sign Up";
        /// <summary>
    /// Gets or sets the Form Action.
    /// </summary>
public string FormAction { get; set; } = "#";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
