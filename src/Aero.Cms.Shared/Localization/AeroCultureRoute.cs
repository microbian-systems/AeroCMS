using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Shared.Localization;

/// <summary>
/// Represents a class for AeroCultureRoute.
/// </summary>
public static class AeroCultureRoute
{
        /// <summary>
    /// CultureItemKey.
    /// </summary>
public const string CultureItemKey = "AeroCms.Culture";
        /// <summary>
    /// CulturePrefixItemKey.
    /// </summary>
public const string CulturePrefixItemKey = "AeroCms.CulturePrefix";
        /// <summary>
    /// IsFallbackCultureItemKey.
    /// </summary>
public const string IsFallbackCultureItemKey = "AeroCms.IsFallbackCulture";

    /// <summary>Gets a value indicating whether a path segment is a supported culture alias.</summary>
    public static bool TryResolveSupportedCultureAlias(string? alias, IEnumerable<string>? supportedCultures, out string culture)
    {
        culture = string.Empty;
        if (string.IsNullOrWhiteSpace(alias)) return false;

        var supported = NormalizeSupportedCultures(supportedCultures, "en-US");
        var exact = supported.FirstOrDefault(x => string.Equals(x, alias.Trim(), StringComparison.OrdinalIgnoreCase));
        if (exact is not null) { culture = exact; return true; }

        // A neutral alias is accepted only when exactly one configured specific culture owns it.
        // This prevents /en from silently selecting between en-US and en-GB.
        if (alias.IndexOf('-') >= 0) return false;
        var matches = supported.Where(x => string.Equals(CultureInfo.GetCultureInfo(x).TwoLetterISOLanguageName, alias.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1) return false;
        culture = matches[0];
        return true;
    }

        /// <summary>
    /// NormalizeCultureOrDefault method.
    /// </summary>
public static string NormalizeCultureOrDefault(string? culture, string fallback = "en-US")
    {
        if (string.IsNullOrWhiteSpace(culture))
            return fallback;

        try
        {
            return CultureInfo.GetCultureInfo(culture.Trim()).Name;
        }
        catch (CultureNotFoundException)
        {
            return fallback;
        }
    }

        /// <summary>
    /// NormalizeSupportedCultures method.
    /// </summary>
public static IReadOnlyList<string> NormalizeSupportedCultures(IEnumerable<string>? cultures, string defaultCulture)
    {
        var normalized = (cultures ?? [])
            .Select(x => NormalizeCultureOrDefault(x, defaultCulture))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!normalized.Contains(defaultCulture, StringComparer.OrdinalIgnoreCase))
            normalized.Insert(0, defaultCulture);

        return normalized;
    }

        /// <summary>
    /// GetLeadingSupportedCulture method.
    /// </summary>
public static string? GetLeadingSupportedCulture(PathString path, IEnumerable<string> supportedCultures)
    {
        var segment = GetLeadingSegment(path.Value);
        if (segment is null)
            return null;

        return TryResolveSupportedCultureAlias(segment, supportedCultures, out var culture) ? culture : null;
    }

        /// <summary>
    /// ResolveRequestCulture method.
    /// </summary>
public static string ResolveRequestCulture(
        PathString path,
        string? defaultCulture,
        IEnumerable<string>? supportedCultures,
        out string? pathCulture)
    {
        var normalizedDefault = NormalizeCultureOrDefault(defaultCulture);
        var normalizedSupportedCultures = NormalizeSupportedCultures(supportedCultures, normalizedDefault);
        pathCulture = GetLeadingSupportedCulture(path, normalizedSupportedCultures);

        return pathCulture ?? normalizedDefault;
    }

        /// <summary>
    /// StripLeadingCulture method.
    /// </summary>
public static string StripLeadingCulture(string? slug, IEnumerable<string>? supportedCultures = null)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return string.Empty;

        var trimmed = slug.Trim().TrimStart('/');
        var slashIndex = trimmed.IndexOf('/');
        var firstSegment = slashIndex < 0 ? trimmed : trimmed[..slashIndex];
        var normalized = NormalizeCultureOrDefault(firstSegment, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
            return trimmed;

        if (supportedCultures is not null &&
            !supportedCultures.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return slashIndex < 0 ? string.Empty : trimmed[(slashIndex + 1)..];
    }

        /// <summary>
    /// BuildCulturePath method.
    /// </summary>
public static string BuildCulturePath(string culture, string? slug)
    {
        var normalizedCulture = NormalizeCultureOrDefault(culture);
        var normalizedSlug = (slug ?? string.Empty).Trim().Trim('/');

        return string.IsNullOrWhiteSpace(normalizedSlug)
            ? $"/{normalizedCulture}"
            : $"/{normalizedCulture}/{normalizedSlug}";
    }

        /// <summary>
    /// BuildCulturePathForCurrentRequest method.
    /// </summary>
public static string BuildCulturePathForCurrentRequest(HttpContext httpContext, string culture, string? slug)
    {
        var pathBase = httpContext.Request.PathBase.Value?.TrimEnd('/') ?? string.Empty;
        return pathBase + BuildCulturePath(culture, slug);
    }

    private static string? GetLeadingSegment(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var trimmed = path.TrimStart('/');
        if (trimmed.Length == 0)
            return null;

        var slashIndex = trimmed.IndexOf('/');
        return slashIndex < 0 ? trimmed : trimmed[..slashIndex];
    }
}
