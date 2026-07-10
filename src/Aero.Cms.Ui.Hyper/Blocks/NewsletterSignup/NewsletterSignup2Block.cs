using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.NewsletterSignup;

/// <summary>
/// HyperUI Newsletter Signup 2 — centered signup form with email input and CTA button.
/// Source: hyperui/public/examples/marketing/newsletter-signup/2.html, 2-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.newsletter-signup.2",
    "Newsletter Signup 2",
    Category = "Hyper",
    Icon = "mail",
    SortOrder = 123,
    SchemaVersion = 1)]
public sealed class NewsletterSignup2Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.newsletter-signup.2";

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
