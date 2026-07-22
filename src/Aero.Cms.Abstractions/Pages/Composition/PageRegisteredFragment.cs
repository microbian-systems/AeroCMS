using System.Text.Json;

namespace Aero.Cms.Abstractions.Pages.Composition;

/// <summary>Identifies a developer-registered application fragment in a page composition.</summary>
/// <remarks>
/// Persisted pages retain only the stable provider key and bounded scalar parameters. They never
/// retain a Razor path, CLR type name, or provider-produced markup.
/// </remarks>
public sealed record PageRegisteredFragment
{
    public const int MaximumFragmentsPerPage = 100;
    public const int MaximumKeyLength = 128;
    public const int MaximumParameterCount = 32;
    public const int MaximumParameterNameLength = 128;
    public const int MaximumParametersUtf8Bytes = 16 * 1024;

    /// <summary>Gets the stable HTML element populated by the registered provider.</summary>
    public long NodeId { get; init; }

    /// <summary>Gets the normalized lowercase dotted/kebab provider key.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Gets the typed scalar parameters accepted by the registered descriptor.</summary>
    public IReadOnlyDictionary<string, JsonElement> Parameters { get; init; }
        = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    /// <summary>Creates an independent persisted snapshot.</summary>
    public PageRegisteredFragment CreateSnapshot() => this with
    {
        Parameters = (Parameters ?? new Dictionary<string, JsonElement>())
            .ToDictionary(parameter => parameter.Key, parameter => parameter.Value.Clone(), StringComparer.Ordinal)
    };

    /// <summary>Normalizes a provider key for deterministic lookup.</summary>
    public static string NormalizeKey(string? key) => (key ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Checks the restricted provider-key grammar without runtime discovery.</summary>
    public static bool IsValidKey(string? key)
    {
        var normalized = NormalizeKey(key);
        if (normalized.Length is 0 or > MaximumKeyLength)
        {
            return false;
        }

        var previousSeparator = true;
        foreach (var character in normalized)
        {
            var separator = character is '.' or '-';
            if (!(character is >= 'a' and <= 'z'
                  || character is >= '0' and <= '9'
                  || separator)
                || (separator && previousSeparator))
            {
                return false;
            }

            previousSeparator = separator;
        }

        return !previousSeparator;
    }
}

/// <summary>Supported scalar parameter kinds for registered application fragments.</summary>
public enum PageRegisteredFragmentParameterKind
{
    String,
    Integer,
    Decimal,
    Boolean,
    Enum
}

/// <summary>Describes one schema-driven parameter exposed by a registered fragment.</summary>
public sealed record PageRegisteredFragmentParameterDescriptor
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public PageRegisteredFragmentParameterKind Kind { get; init; }
    public bool Required { get; init; }
    public JsonElement? DefaultValue { get; init; }
    public int? MaximumLength { get; init; }
    public decimal? Minimum { get; init; }
    public decimal? Maximum { get; init; }
    public IReadOnlyList<string> Choices { get; init; } = [];
}

/// <summary>Catalog metadata for one explicitly registered application fragment.</summary>
public sealed record PageRegisteredFragmentDescriptor
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Category { get; init; } = "Registered";
    public IReadOnlyList<PageRegisteredFragmentParameterDescriptor> Parameters { get; init; } = [];
}
