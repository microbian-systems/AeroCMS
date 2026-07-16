namespace Aero.Cms.Core.Infrastructure;

/// <summary>
/// Normalizes hostnames for consistent site resolution.
/// Used by middleware, repository queries, validators, and seed logic.
/// </summary>
public static class HostNormalizer
{
        /// <summary>
    /// Normalize method.
    /// </summary>
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
