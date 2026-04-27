using System.Text.Json.Serialization;
using Aero.Cms.Abstractions.Blocks.Common;

namespace Aero.Cms.Abstractions.Blocks.Serialization;

/// <summary>
/// Source-generated JSON serializer context for AOT-compatible block serialization.
/// </summary>
/// <remarks>
/// This context is designed to be regenerated when new block types are added.
/// The source generator will automatically include all types decorated with
/// [JsonDerivedType] on BlockBase subclasses when BlockBase is registered.
/// </remarks>
[JsonSerializable(typeof(BlockBase))]
[JsonSerializable(typeof(List<BlockBase>))]

// Supporting Models
[JsonSerializable(typeof(ColumnItem))]
[JsonSerializable(typeof(List<ColumnItem>))]
[JsonSerializable(typeof(CarouselItem))]
[JsonSerializable(typeof(List<CarouselItem>))]
[JsonSerializable(typeof(ScrollingContentItem))]
[JsonSerializable(typeof(List<ScrollingContentItem>))]
[JsonSerializable(typeof(FormFieldDefinition))]
[JsonSerializable(typeof(List<FormFieldDefinition>))]
[JsonSerializable(typeof(OrderedDictionary<ushort, FormFieldDefinition>))]
[JsonSerializable(typeof(NavigationBlock.NavigationBlockItem))]
[JsonSerializable(typeof(List<NavigationBlock.NavigationBlockItem>))]
[JsonSerializable(typeof(OrderedDictionary<ushort, NavigationBlock.NavigationBlockItem>))]
[JsonSerializable(typeof(AeroButton))]
[JsonSerializable(typeof(List<AeroButton>))]

// Explicit List registrations for all concrete blocks to ensure they can be serialized as collections
[JsonSerializable(typeof(List<RichTextBlock>))]
[JsonSerializable(typeof(List<HeadingBlock>))]
[JsonSerializable(typeof(List<ImageBlock>))]
[JsonSerializable(typeof(List<CtaBlock>))]
[JsonSerializable(typeof(List<QuoteBlock>))]
[JsonSerializable(typeof(List<EmbedBlock>))]
[JsonSerializable(typeof(List<YouTubeBlock>))]
[JsonSerializable(typeof(List<VimeoBlock>))]
[JsonSerializable(typeof(List<TwitchBlock>))]
[JsonSerializable(typeof(List<TikTokBlock>))]
[JsonSerializable(typeof(List<ColumnsBlock>))]
[JsonSerializable(typeof(List<CardBlock>))]
[JsonSerializable(typeof(List<CarouselBlock>))]
[JsonSerializable(typeof(List<ContentLinkBlock>))]
[JsonSerializable(typeof(List<HeroBlock>))]
[JsonSerializable(typeof(List<MarkdownBlock>))]
[JsonSerializable(typeof(List<RawHtmlBlock>))]
[JsonSerializable(typeof(List<AnalyticsBlock>))]
[JsonSerializable(typeof(List<ScrollingContentBlock>))]
[JsonSerializable(typeof(List<FormEditorBlock>))]
[JsonSerializable(typeof(List<NavigationBlock>))]
[JsonSerializable(typeof(List<AeroHeroBlock>))]
[JsonSerializable(typeof(List<AeroFeaturesBlock>))]
[JsonSerializable(typeof(List<AeroFeatureItem>))]
[JsonSerializable(typeof(List<AeroCtaBlock>))]
[JsonSerializable(typeof(List<AeroBlogBlock>))]
[JsonSerializable(typeof(List<AeroBlogItem>))]
[JsonSerializable(typeof(AeroBlogItem))]
[JsonSerializable(typeof(List<AeroPricingBlock>))]
[JsonSerializable(typeof(List<AeroPricingPlan>))]
[JsonSerializable(typeof(AeroPricingPlan))]
[JsonSerializable(typeof(List<AeroTeamsBlock>))]
[JsonSerializable(typeof(List<AeroTeamMember>))]
[JsonSerializable(typeof(AeroTeamMember))]
[JsonSerializable(typeof(List<AeroTestimonialsBlock>))]
[JsonSerializable(typeof(List<AeroTestimonialItem>))]
[JsonSerializable(typeof(AeroTestimonialItem))]


// Concrete types (Only needed if used directly outside of BlockBase polymorphism)
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
[JsonSerializable(typeof(HeroBlock))]
[JsonSerializable(typeof(MarkdownBlock))]
[JsonSerializable(typeof(RawHtmlBlock))]
[JsonSerializable(typeof(AnalyticsBlock))]
[JsonSerializable(typeof(ScrollingContentBlock))]
[JsonSerializable(typeof(FormEditorBlock))]
[JsonSerializable(typeof(NavigationBlock))]
[JsonSerializable(typeof(AeroHeroBlock))]
[JsonSerializable(typeof(AeroFeaturesBlock))]
[JsonSerializable(typeof(AeroCtaBlock))]
[JsonSerializable(typeof(AeroBlogBlock))]
[JsonSerializable(typeof(AeroPricingBlock))]
[JsonSerializable(typeof(AeroTeamsBlock))]
[JsonSerializable(typeof(AeroTestimonialsBlock))]
[JsonSerializable(typeof(AeroFaqBlock))]
[JsonSerializable(typeof(AeroPortfolioBlock))]
[JsonSerializable(typeof(AeroContactBlock))]
[JsonSerializable(typeof(AeroTableBlock))]
[JsonSerializable(typeof(AeroAuthBlock))]

// Explicit List registrations
[JsonSerializable(typeof(List<AeroFaqBlock>))]
[JsonSerializable(typeof(List<AeroFaqItem>))]
[JsonSerializable(typeof(List<AeroPortfolioBlock>))]
[JsonSerializable(typeof(List<AeroPortfolioItem>))]
[JsonSerializable(typeof(List<AeroContactBlock>))]
[JsonSerializable(typeof(List<AeroContactDetail>))]
[JsonSerializable(typeof(List<AeroTableBlock>))]
[JsonSerializable(typeof(List<AeroTableHeader>))]
[JsonSerializable(typeof(List<AeroTableRow>))]
[JsonSerializable(typeof(List<AeroAuthBlock>))]

// Common Primitives and System Types
[JsonSerializable(typeof(Dictionary<string, string>))]
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
