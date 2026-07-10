using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Faqs;

/// <summary>
/// HyperUI FAQ 3 — left-border-accented <c>&lt;details&gt;</c> accordion (border-s-4).
/// Source: hyperui/public/examples/marketing/faqs/3.html, 3-dark.html.
/// </summary>
[BlockMetadata(
    "hyper.faqs.3",
    "FAQ 3",
    Category = "Hyper",
    Icon = "help-circle",
    SortOrder = 72,
    SchemaVersion = 1)]
public sealed class Faq3Block : BlockBase
{
        /// <summary>
    /// BlockTypeId.
    /// </summary>
public const string BlockTypeId = "hyper.faqs.3";

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public override string BlockType => BlockTypeId;

        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; set; } = "FAQs";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description { get; set; } = "";
        /// <summary>
    /// Gets or sets the Items.
    /// </summary>
public List<AeroFaqItem> Items { get; set; } = DefaultItems.Select(CloneItem).ToList();

        /// <summary>
    /// DefaultItems.
    /// </summary>
public static readonly List<AeroFaqItem> DefaultItems =
    [
        new() { Question = "Lorem ipsum dolor sit amet consectetur adipisicing?", Answer = "Lorem ipsum dolor sit amet consectetur, adipisicing elit. Ab hic veritatis molestias culpa in, recusandae laboriosam neque aliquid libero nesciunt voluptate dicta quo officiis explicabo consequuntur distinctio corporis earum similique!" },
        new() { Question = "Lorem ipsum dolor sit amet consectetur adipisicing?", Answer = "Lorem ipsum dolor sit amet consectetur, adipisicing elit. Ab hic veritatis molestias culpa in, recusandae laboriosam neque aliquid libero nesciunt voluptate dicta quo officiis explicabo consequuntur distinctio corporis earum similique!" },
        new() { Question = "Lorem ipsum dolor sit amet consectetur adipisicing?", Answer = "Lorem ipsum dolor sit amet consectetur, adipisicing elit. Ab hic veritatis molestias culpa in, recusandae laboriosam neque aliquid libero nesciunt voluptate dicta quo officiis explicabo consequuntur distinctio corporis earum similique!" }
    ];

        /// <summary>
    /// Accept method.
    /// </summary>
public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);

    private static AeroFaqItem CloneItem(AeroFaqItem item) => new()
    {
        Question = item.Question,
        Answer = item.Answer
    };
}
