using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Sections;

/// <summary>
/// HyperUI Sections 4 — stacked layout with text on top, image below.
/// Source: hyperui/public/examples/marketing/sections/4.html (light-only).
/// </summary>
[BlockMetadata(
    "hyper.sections.4",
    "Sections 4",
    Category = "Hyper",
    Icon = "columns",
    SortOrder = 80,
    SchemaVersion = 1)]
public sealed class Sections4Block : BlockBase
{
    public const string BlockTypeId = "hyper.sections.4";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Lorem ipsum dolor sit amet consectetur adipisicing elit.";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Tenetur doloremque saepe architecto maiores repudiandae amet perferendis repellendus, reprehenderit voluptas sequi.";
    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1731690415686-e68f78e2b5bd?auto=format&fit=crop&q=80&w=1160";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
