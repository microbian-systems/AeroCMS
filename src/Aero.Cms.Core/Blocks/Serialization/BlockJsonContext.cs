using System.Text.Json;
using System.Text.Json.Serialization;
using Aero.Cms.Core.Blocks.Common;

namespace Aero.Cms.Core.Blocks.Serialization;

/// <summary>
/// Source-generated JSON serializer context for AOT-compatible block serialization.
/// </summary>
/// <remarks>
/// This context is designed to be regenerated when new block types are added.
/// The source generator will automatically include all types decorated with
/// [JsonDerivedType] on BlockBase subclasses.
/// </remarks>
[JsonSerializable(typeof(RichTextBlock))]
[JsonSerializable(typeof(HeadingBlock))]
[JsonSerializable(typeof(ImageBlock))]
[JsonSerializable(typeof(CtaBlock))]
[JsonSerializable(typeof(QuoteBlock))]
[JsonSerializable(typeof(EmbedBlock))]
[JsonSerializable(typeof(YouTubeBlock))]
[JsonSerializable(typeof(VimeoBlock))]
[JsonSerializable(typeof(TwitchBlock))]
[JsonSerializable(typeof(TikTokBlock))]
[JsonSerializable(typeof(ColumnsBlock))]
[JsonSerializable(typeof(CardBlock))]
[JsonSerializable(typeof(CarouselBlock))]
[JsonSerializable(typeof(ContentLinkBlock))]
[JsonSerializable(typeof(ColumnItem))]
[JsonSerializable(typeof(CarouselItem))]
[JsonSerializable(typeof(BlockBase))]
[JsonSerializable(typeof(List<BlockBase>))]
[JsonSerializable(typeof(List<YouTubeBlock>))]
[JsonSerializable(typeof(List<VimeoBlock>))]
[JsonSerializable(typeof(List<TwitchBlock>))]
[JsonSerializable(typeof(List<TikTokBlock>))]
[JsonSerializable(typeof(List<ColumnsBlock>))]
[JsonSerializable(typeof(List<CardBlock>))]
[JsonSerializable(typeof(List<CarouselBlock>))]
[JsonSerializable(typeof(List<ContentLinkBlock>))]
[JsonSerializable(typeof(List<HeroBlock>))]
[JsonSerializable(typeof(HeroBlock))]
[JsonSerializable(typeof(List<NavigationBlock>))]
[JsonSerializable(typeof(NavigationBlock))]
[JsonSerializable(typeof(NavigationBlock.NavigationBlockItem))]
[JsonSerializable(typeof(OrderedDictionary<ushort, NavigationBlock.NavigationBlockItem>))]
[JsonSerializable(typeof(List<ColumnItem>))]
[JsonSerializable(typeof(List<CarouselItem>))]
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
