using System.Text.Json;

namespace Aero.Cms.Abstractions.Blocks.Common;

public sealed record ContentEmbedFieldMapping(
    string FieldName,
    string ComponentAlias,
    Dictionary<string, JsonElement>? Props = null
);
