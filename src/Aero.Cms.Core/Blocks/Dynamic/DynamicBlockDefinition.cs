using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Core.Entities;

namespace Aero.Cms.Core.Blocks.Dynamic;

/// <summary>
/// Stores a user-authored dynamic block template definition.
/// </summary>
public sealed class DynamicBlockDefinition : Entity
{
        /// <summary>
    /// Gets or sets the Content Type Id.
    /// </summary>
public long? ContentTypeId { get; set; }

        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long? SiteId { get; set; }

        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = string.Empty;

        /// <summary>
    /// Gets or sets the Block Type.
    /// </summary>
public string BlockType { get; set; } = DynamicTemplateBlock.Discriminator;

        /// <summary>
    /// Gets or sets the Scriban Template.
    /// </summary>
public string ScribanTemplate { get; set; } = string.Empty;

        /// <summary>
    /// Gets or sets the Data Schema.
    /// </summary>
public JsonDocument? DataSchema { get; set; }

        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public int Version { get; set; } = 1;

        /// <summary>
    /// Gets or sets the Is Published.
    /// </summary>
public bool IsPublished { get; set; }
}
