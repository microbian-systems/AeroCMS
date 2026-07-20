using System.Text.Json.Serialization;
using Aero.Cms.Modules.Navigation.Domain;

namespace Aero.Cms.Modules.Navigation.Serialization;

/// <summary>
/// Provides source-generated System.Text.Json metadata for navigation documents and polymorphic components.
/// </summary>
/// <remarks>
/// Payloads use camel-case property names, omit null values, and encode component types through
/// the discriminator configured on <see cref="INavMenuComponent"/>.
/// </remarks>
[JsonSerializable(typeof(NavMenuDocument))]
[JsonSerializable(typeof(SiteNavigationSettingsDocument))]
[JsonSerializable(typeof(NavMenuSnapshot))]
[JsonSerializable(typeof(NavCanvasRow))]
[JsonSerializable(typeof(NavCanvasColumn))]
[JsonSerializable(typeof(NavCanvasBlock))]
[JsonSerializable(typeof(List<INavMenuComponent>))]
[JsonSerializable(typeof(INavMenuComponent[]))]
[JsonSerializable(typeof(NavLink))]
[JsonSerializable(typeof(NavMenu))]
[JsonSerializable(typeof(NavHtml))]
[JsonSerializable(typeof(NavSearch))]
[JsonSerializable(typeof(NavLanguageSelect))]
[JsonSerializable(typeof(NavAuthButton))]
[JsonSerializable(typeof(NavAuthVisibility))]
[JsonSerializable(typeof(NavItemVisibility))]
[JsonSerializable(typeof(NavMenuLayout))]
[JsonSerializable(typeof(NavLayoutSlot))]
[JsonSerializable(typeof(NavMenuResponsiveSettings))]
[JsonSerializable(typeof(NavMenuStyleSettings))]
[JsonSerializable(typeof(NavMenuLifecycleState))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default | JsonSourceGenerationMode.Metadata)]
public partial class NavMenuJsonContext : JsonSerializerContext
{
}
