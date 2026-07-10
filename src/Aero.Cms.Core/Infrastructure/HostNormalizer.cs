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

        return host.Trim().ToLowerInvariant().TrimEnd('.');
    }
}
