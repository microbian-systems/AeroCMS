using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Neo.Blocks.Newsletter;

[BlockMetadata(
    "neo.newsletter",
    "Newsletter Signup",
    Category = "Neo",
    Icon = "mail",
    SortOrder = 40,
    SchemaVersion = 1)]
public sealed class NewsletterBlock : BlockBase
{
    public const string BlockTypeId = "neo.newsletter";
    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Stay in the loop";
    public string Description { get; set; } =
        "Get the latest news, product updates, and tips delivered straight to your inbox.";
    public string Placeholder { get; set; } = "Enter your email";
    public string ButtonText { get; set; } = "Subscribe";
    public string PrivacyText { get; set; } = "We respect your privacy. Unsubscribe at any time.";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
