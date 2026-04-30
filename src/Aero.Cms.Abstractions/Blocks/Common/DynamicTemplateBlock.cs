using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// A persisted block that renders data through a versioned dynamic Scriban template definition.
/// </summary>
[BlockMetadata("dynamic_template", "Dynamic Template", Category = "Dynamic")]
public sealed class DynamicTemplateBlock : BlockBase
{
    public const string Discriminator = "dynamic_template";

    public override string BlockType => Discriminator;

    /// <summary>
    /// Gets or sets the dynamic template definition id.
    /// </summary>
    public long DefinitionId { get; set; }

    /// <summary>
    /// Gets or sets the template version this block should render with.
    /// </summary>
    public int DefinitionVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets an inline template used by the MVP editor path.
    /// When set, rendering does not require a persisted template definition.
    /// </summary>
    public string? InlineTemplate { get; set; }

    /// <summary>
    /// Gets or sets the runtime JSON data passed to the template as the <c>block</c> variable.
    /// </summary>
    public JsonDocument? Data { get; set; }

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
