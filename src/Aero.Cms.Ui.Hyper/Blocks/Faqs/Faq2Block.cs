using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Faqs;

/// <summary>
/// HyperUI FAQ 2 — divider-separated <c>&lt;details&gt;</c> accordion (divide-y layout).
/// Source: hyperui/public/examples/marketing/faqs/2.html, 2-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.faqs.2",
    "FAQ 2",
    Category = "Hyper",
    Icon = "help-circle",
    SortOrder = 71,
    SchemaVersion = 1)]
public sealed class Faq2Block : BlockBase
{
    public const string BlockTypeId = "hyper.faqs.2";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "FAQs";
    public string Description { get; set; } = "";
    public List<AeroFaqItem> Items { get; set; } = DefaultItems.Select(CloneItem).ToList();

    public static readonly List<AeroFaqItem> DefaultItems =
    [
        new() { Question = "Lorem ipsum dolor sit amet consectetur adipisicing?", Answer = "Lorem ipsum dolor sit amet consectetur, adipisicing elit. Ab hic veritatis molestias culpa in, recusandae laboriosam neque aliquid libero nesciunt voluptate dicta quo officiis explicabo consequuntur distinctio corporis earum similique!" },
        new() { Question = "Lorem ipsum dolor sit amet consectetur adipisicing?", Answer = "Lorem ipsum dolor sit amet consectetur, adipisicing elit. Ab hic veritatis molestias culpa in, recusandae laboriosam neque aliquid libero nesciunt voluptate dicta quo officiis explicabo consequuntur distinctio corporis earum similique!" },
        new() { Question = "Lorem ipsum dolor sit amet consectetur adipisicing?", Answer = "Lorem ipsum dolor sit amet consectetur, adipisicing elit. Ab hic veritatis molestias culpa in, recusandae laboriosam neque aliquid libero nesciunt voluptate dicta quo officiis explicabo consequuntur distinctio corporis earum similique!" }
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static AeroFaqItem CloneItem(AeroFaqItem item) => new()
    {
        Question = item.Question,
        Answer = item.Answer
    };
}
