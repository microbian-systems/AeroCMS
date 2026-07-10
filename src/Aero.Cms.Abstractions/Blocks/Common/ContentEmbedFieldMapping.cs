namespace Aero.Cms.Abstractions.Blocks.Common;

/// <summary>
/// Represents a record for ContentEmbedFieldMapping.
/// </summary>
public sealed record ContentEmbedFieldMapping(
    string FieldName,
    string ComponentAlias,
    Dictionary<string, JsonElement>? Props = null
);
