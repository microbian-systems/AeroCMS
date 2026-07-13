namespace Aero.Cms.Html;

/// <summary>
/// Shared URL-scheme validation for persisted HTML attributes and CSS media surfaces.
/// </summary>
internal static class HtmlUrlPolicy
{
    public static bool IsSafeNavigationUrl(string value) =>
        IsSafeUrl(value, allowFragment: true, Uri.UriSchemeMailto, "tel");

    public static bool IsSafeMediaUrl(string value) =>
        IsSafeUrl(value, allowFragment: false);

    private static bool IsSafeUrl(string value, bool allowFragment, params string[] additionalSchemes)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(character => char.IsControl(character) || character == '\\'))
        {
            return false;
        }

        if (allowFragment && value.StartsWith('#'))
        {
            return true;
        }

        if (!Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var uri))
        {
            return false;
        }

        if (!uri.IsAbsoluteUri)
        {
            return true;
        }

        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || additionalSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);
    }
}
