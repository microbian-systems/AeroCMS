using System.Text.Json;
using System.Text.Json.Serialization;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Content.Localization;

namespace Aero.Cms.Abstractions.Content.Serialization;

/// <summary>
/// Source-generated JSON metadata for Content Type field bags and editor values.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, JsonElement>))]
[JsonSerializable(typeof(List<ContentFieldDefinition>))]
[JsonSerializable(typeof(ContentFieldDefinition))]
#if !AERO_CMS_BROWSER_CLIENT
[JsonSerializable(typeof(ContentTypeDefinition))]
[JsonSerializable(typeof(ContentItem))]
#endif
[JsonSerializable(typeof(ContentLocalizationSettings))]
[JsonSerializable(typeof(ContentTranslationGroup))]
[JsonSerializable(typeof(ContentTranslationProvenance))]
[JsonSerializable(typeof(ContentTranslationReview))]
[JsonSerializable(typeof(ContentLocalizationContext))]
[JsonSerializable(typeof(ContentCultureForkCommand))]
[JsonSerializable(typeof(ApplyContentAiTranslationCommand))]
[JsonSerializable(typeof(ReviewContentTranslationCommand))]
[JsonSerializable(typeof(ContentLocalizationOperationResult))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(decimal?))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(DateTime?))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<decimal>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, decimal>))]
[JsonSerializable(typeof(CmsContentReferenceValue))]
[JsonSerializable(typeof(ContentEntryKey))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default | JsonSourceGenerationMode.Metadata)]
public partial class ContentJsonContext : JsonSerializerContext;
