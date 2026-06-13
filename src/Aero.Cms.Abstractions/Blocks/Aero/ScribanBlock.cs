using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

[BlockMetadata("neo.template.scriban", "Scriban Template", Category = "Dynamic", Icon = "code", SortOrder = 100, SchemaVersion = 1)]
public sealed class ScribanBlock : BlockBase
{
    public override string BlockType => "neo.template.scriban";

    /// <summary>Display name for the editor.</summary>
    public string Name { get; set; } = "Scriban Block";

    /// <summary>The Scriban template text.</summary>
    public string Template { get; set; } = string.Empty;

    /// <summary>JSON data passed as the `block` variable.</summary>
    public JsonDocument? Data { get; set; }

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
