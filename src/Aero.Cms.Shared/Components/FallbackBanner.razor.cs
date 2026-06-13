using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Components;

public partial class FallbackBanner : ComponentBase
{
    [Parameter]
    public bool Show { get; set; }

    [Parameter]
    public string RequestedCulture { get; set; } = CultureInfo.CurrentUICulture.Name;

    [Parameter]
    public string RenderedCulture { get; set; } = CultureInfo.CurrentUICulture.Name;

    protected string RequestedCultureDisplayName => GetCultureDisplayName(RequestedCulture);

    protected string RenderedCultureDisplayName => GetCultureDisplayName(RenderedCulture);

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
