using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Components;

/// <summary>
/// Represents a class for FallbackBanner.
/// </summary>
public partial class FallbackBanner : ComponentBase
{
        /// <summary>
    /// Gets or sets the Show.
    /// </summary>
[Parameter]
    public bool Show { get; set; }

        /// <summary>
    /// Gets or sets the Requested Culture.
    /// </summary>
[Parameter]
    public string RequestedCulture { get; set; } = CultureInfo.CurrentUICulture.Name;

        /// <summary>
    /// Gets or sets the Rendered Culture.
    /// </summary>
[Parameter]
    public string RenderedCulture { get; set; } = CultureInfo.CurrentUICulture.Name;

        /// <summary>
    /// Gets or sets the Requested Culture Display Name.
    /// </summary>
protected string RequestedCultureDisplayName => GetCultureDisplayName(RequestedCulture);

        /// <summary>
    /// Gets or sets the Rendered Culture Display Name.
    /// </summary>
protected string RenderedCultureDisplayName => GetCultureDisplayName(RenderedCulture);

        /// <summary>
    /// Gets or sets the Dismiss Cookie Name.
    /// </summary>
protected string DismissCookieName =>
        $"AeroCms.LocalizationFallback.{NormalizeCookiePart(RequestedCulture)}.{NormalizeCookiePart(RenderedCulture)}";

    private static string GetCultureDisplayName(string culture)
    {
        try
        {
            return CultureInfo.GetCultureInfo(culture).DisplayName;
        }
        catch (CultureNotFoundException)
        {
            return culture;
        }
    }

    private static string NormalizeCookiePart(string culture)
        => string.IsNullOrWhiteSpace(culture)
            ? "unknown"
            : culture.Trim().ToLowerInvariant();
}
