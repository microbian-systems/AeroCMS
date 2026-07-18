using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Content.Serialization;

/// <summary>
/// Source-generated JSON metadata for Content Type field bags and editor values.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, JsonElement>))]
[JsonSerializable(typeof(List<ContentFieldDefinition>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(decimal?))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(DateTime?))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default | JsonSourceGenerationMode.Metadata)]
public partial class ContentJsonContext : JsonSerializerContext;
