using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Neo.Blocks.Newsletter;

/// <summary>
/// Represents a class for NewsletterBlock.
/// </summary>
[BlockMetadata(
    "neo.newsletter",
    "Newsletter Signup",
    Category = "Neo",
    Icon = "mail",
    SortOrder = 40,
    SchemaVersion = 1)]
public sealed class NewsletterBlock : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "neo.newsletter";
        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "Stay in the loop";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } =
        "Get the latest news, product updates, and tips delivered straight to your inbox.";
        /// <summary>
    /// Gets or sets the Placeholder.
    /// </summary>
public string Placeholder { get; set; } = "Enter your email";
        /// <summary>
    /// Gets or sets the Button Text.
    /// </summary>
public string ButtonText { get; set; } = "Subscribe";
        /// <summary>
    /// Gets or sets the Privacy Text.
    /// </summary>
public string PrivacyText { get; set; } = "We respect your privacy. Unsubscribe at any time.";

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
