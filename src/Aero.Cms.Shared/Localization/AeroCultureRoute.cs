using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Shared.Localization;

public static class AeroCultureRoute
{
    public const string CultureItemKey = "AeroCms.Culture";
    public const string CulturePrefixItemKey = "AeroCms.CulturePrefix";
    public const string IsFallbackCultureItemKey = "AeroCms.IsFallbackCulture";

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

    public static string? GetLeadingSupportedCulture(PathString path, IEnumerable<string> supportedCultures)
    {
        var segment = GetLeadingSegment(path.Value);
        if (segment is null)
            return null;

        var normalizedSegment = NormalizeCultureOrDefault(segment, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedSegment))
            return null;

        return supportedCultures.Contains(normalizedSegment, StringComparer.OrdinalIgnoreCase)
            ? normalizedSegment
            : null;
    }

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

    public static string BuildCulturePath(string culture, string? slug)
    {
        var normalizedCulture = NormalizeCultureOrDefault(culture).ToLowerInvariant();
        var normalizedSlug = (slug ?? string.Empty).Trim().Trim('/');

        return string.IsNullOrWhiteSpace(normalizedSlug)
            ? $"/{normalizedCulture}"
            : $"/{normalizedCulture}/{normalizedSlug}";
    }

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
