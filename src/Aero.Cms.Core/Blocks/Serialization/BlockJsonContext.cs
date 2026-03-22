using System.Text.Json;
using System.Text.Json.Serialization;
using Aero.Core.Entities;

namespace Aero.Cms.Core.Blocks.Serialization;

/// <summary>
/// Source-generated JSON serializer context for AOT-compatible block serialization.
/// </summary>
/// <remarks>
/// This context is designed to be regenerated when new block types are added.
/// The source generator will automatically include all types decorated with
/// [JsonDerivedType] on BlockBase subclasses.
/// </remarks>
[JsonSerializable(typeof(BlockBase))]
[JsonSerializable(typeof(RichTextBlock))]
[JsonSerializable(typeof(HeadingBlock))]
[JsonSerializable(typeof(ImageBlock))]
[JsonSerializable(typeof(CtaBlock))]
[JsonSerializable(typeof(QuoteBlock))]
[JsonSerializable(typeof(EmbedBlock))]
[JsonSerializable(typeof(List<BlockBase>))]
[JsonSerializable(typeof(List<RichTextBlock>))]
[JsonSerializable(typeof(List<HeadingBlock>))]
[JsonSerializable(typeof(List<ImageBlock>))]
[JsonSerializable(typeof(List<CtaBlock>))]
[JsonSerializable(typeof(List<QuoteBlock>))]
[JsonSerializable(typeof(List<EmbedBlock>))]
[JsonSerializable(typeof(Dictionary<string, BlockBase>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(JsonElement))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default | JsonSourceGenerationMode.Metadata)]
internal partial class BlockJsonContext : JsonSerializerContext
{
}
