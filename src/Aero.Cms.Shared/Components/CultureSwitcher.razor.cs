using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Components;

/// <summary>
/// Represents a class for CultureSwitcher.
/// </summary>
public partial class CultureSwitcher : ComponentBase
{
        /// <summary>
    /// Gets or sets the Links.
    /// </summary>
[Parameter]
    public IReadOnlyList<CultureSwitcherLink> Links { get; set; } = [];

        /// <summary>
    /// Gets or sets the Css Class.
    /// </summary>
[Parameter]
    public string CssClass { get; set; } = string.Empty;

        /// <summary>
    /// CreateLink method.
    /// </summary>
public static CultureSwitcherLink CreateLink(string culture, string href, bool isActive)
    {
        var normalizedCulture = NormalizeCulture(culture);
        return new CultureSwitcherLink(
            normalizedCulture.ToLowerInvariant(),
            BuildShortLabel(normalizedCulture),
            href,
            isActive);
    }

    private static string NormalizeCulture(string culture)
    {
        try
        {
            return CultureInfo.GetCultureInfo(culture).Name;
        }
        catch (CultureNotFoundException)
        {
            return culture;
        }
    }

    private static string BuildShortLabel(string culture)
    {
        try
        {
            var info = CultureInfo.GetCultureInfo(culture);
            return info.TwoLetterISOLanguageName.ToUpperInvariant();
        }
        catch (CultureNotFoundException)
        {
            return culture;
        }
    }
}

/// <summary>
/// Represents a record for CultureSwitcherLink.
/// </summary>
public sealed record CultureSwitcherLink(
    string Hreflang,
    string Label,
    string Href,
    bool IsActive);
