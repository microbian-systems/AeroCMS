using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Pages.Composition;

/// <summary>
/// Source-generated JSON metadata for page-composition snapshot transport.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PageCompositionDocument))]
public sealed partial class PageCompositionJsonContext : JsonSerializerContext;
