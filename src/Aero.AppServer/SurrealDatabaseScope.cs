using System.Text.RegularExpressions;

namespace Aero.AppServer;

/// <summary>
/// Validates installation-wide SurrealDB namespace and database identifiers.
/// </summary>
public static partial class SurrealDatabaseScope
{
    /// <summary>The maximum supported namespace or database name length.</summary>
    public const int MaximumNameLength = 128;

    /// <summary>
    /// Trims and validates a namespace or database name.
    /// </summary>
    /// <param name="value">The configured name.</param>
    /// <param name="normalized">The trimmed name when valid; otherwise an empty string.</param>
    /// <returns><see langword="true"/> when the value is a supported SurrealDB identifier.</returns>
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= MaximumNameLength
               && NamePattern().IsMatch(normalized);
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
