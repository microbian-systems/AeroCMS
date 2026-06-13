using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Core.Entities;

namespace Aero.Cms.Core.Blocks.Dynamic;

/// <summary>
/// Stores a user-authored dynamic block template definition.
/// </summary>
public sealed class DynamicBlockDefinition : Entity
{
    public long? ContentTypeId { get; set; }

    public long? SiteId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string BlockType { get; set; } = DynamicTemplateBlock.Discriminator;

    public string ScribanTemplate { get; set; } = string.Empty;

    public JsonDocument? DataSchema { get; set; }

    public int Version { get; set; } = 1;

    public bool IsPublished { get; set; }
}
