using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Hyper.Blocks.Sections;

/// <summary>
/// HyperUI Sections 1 — 2-column grid with text left, image right.
/// Source: hyperui/public/examples/marketing/sections/1.html (light-only).
/// </summary>
[BlockMetadata(
    "hyper.sections.1",
    "Sections 1",
    Category = "Hyper",
    Icon = "columns",
    SortOrder = 77,
    SchemaVersion = 1)]
public sealed class Sections1Block : BlockBase
{
    public const string BlockTypeId = "hyper.sections.1";

    public override string BlockType => BlockTypeId;

    public string Title { get; set; } = "Lorem ipsum dolor sit amet consectetur adipisicing elit.";
    public string Description { get; set; } = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Tenetur doloremque saepe architecto maiores repudiandae amet perferendis repellendus, reprehenderit voluptas sequi.";
    public string ImageUrl { get; set; } = "https://images.unsplash.com/photo-1731690415686-e68f78e2b5bd?auto=format&fit=crop&q=80&w=1160";

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
