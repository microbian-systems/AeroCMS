namespace Aero.Cms.Core.Infrastructure;

/// <summary>
/// Normalizes hostnames for consistent site resolution.
/// Used by middleware, repository queries, validators, and seed logic.
/// </summary>
/// <remarks>
/// Normalization is not host authorization, origin validation, or an allowlist check. Callers
/// must apply their own security policy to untrusted host input.
/// </remarks>
public static class HostNormalizer
{
    /// <summary>
    /// Produces a case-insensitive comparison form for a host name.
    /// </summary>
    /// <param name="host">The host value to normalize.</param>
    /// <returns>
    /// An empty string for null or whitespace input. Parseable input returns the lowercase IDN
    /// host without a trailing dot; fallback input is trimmed, lowercased, and stripped of
    /// trailing dots.
    /// </returns>
    public static string Normalize(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return string.Empty;

        var normalized = host.Trim().ToLowerInvariant();
        if (Uri.TryCreate($"http://{normalized}", UriKind.Absolute, out var uri))
            return uri.IdnHost.TrimEnd('.');

        return normalized.TrimEnd('.');
    }
}
