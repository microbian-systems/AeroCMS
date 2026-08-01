using System.Text.Json.Serialization;

namespace Aero.Cms.Html;

/// <summary>
/// Source-generated JSON metadata for living-standard page content transported
/// through HTTP and Orleans request boundaries.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(HtmlPageContent))]
[JsonSerializable(typeof(HtmlNode))]
[JsonSerializable(typeof(HtmlStyle))]
public sealed partial class HtmlJsonContext : JsonSerializerContext;
