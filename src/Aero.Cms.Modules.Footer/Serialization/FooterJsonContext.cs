using System.Text.Json.Serialization;
using Aero.Cms.Modules.Footer.Domain;

namespace Aero.Cms.Modules.Footer.Serialization;

[JsonSerializable(typeof(FooterDocument))]
[JsonSerializable(typeof(SiteFooterSettingsDocument))]
[JsonSerializable(typeof(FooterSnapshot))]
[JsonSerializable(typeof(FooterCanvasRow))]
[JsonSerializable(typeof(FooterCanvasColumn))]
[JsonSerializable(typeof(FooterCanvasBlock))]
[JsonSerializable(typeof(List<IFooterComponent>))]
[JsonSerializable(typeof(IFooterComponent[]))]
[JsonSerializable(typeof(FooterLinkGroup))]
[JsonSerializable(typeof(FooterTextBlock))]
[JsonSerializable(typeof(FooterSocialLinks))]
[JsonSerializable(typeof(FooterNewsletterSignup))]
[JsonSerializable(typeof(FooterSearch))]
[JsonSerializable(typeof(FooterSpacer))]
[JsonSerializable(typeof(FooterBrandSettings))]
[JsonSerializable(typeof(FooterStyleSettings))]
[JsonSerializable(typeof(FooterResponsiveSettings))]
[JsonSerializable(typeof(FooterLegalSettings))]
[JsonSerializable(typeof(FooterLink))]
[JsonSerializable(typeof(FooterSocialLink))]
[JsonSerializable(typeof(FooterLifecycleState))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default | JsonSourceGenerationMode.Metadata)]
public partial class FooterJsonContext : JsonSerializerContext
{
}
