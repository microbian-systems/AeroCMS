namespace Aero.Cms.Abstractions.Pages.Rendering;

/// <summary>A strongly typed renderer identifier used by server-side strategy contracts.</summary>
/// <param name="Value">The stable persisted identifier value.</param>
public readonly record struct PageRendererId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// Stable renderer identifiers persisted with page metadata.
/// </summary>
public static class PageRendererIds
{
    /// <summary>The visual Aero composition renderer.</summary>
    public const string AeroComposition = "aero.composition";

    /// <summary>The full-page Scriban renderer.</summary>
    public const string Scriban = "aero.scriban";

    /// <summary>The full-page SharpTS renderer.</summary>
    public const string SharpTs = "aero.sharpts";

    /// <summary>The full-page HTMX renderer.</summary>
    public const string Htmx = "aero.htmx";

    /// <summary>Maximum persisted renderer identifier length.</summary>
    public const int MaximumLength = 80;

    /// <summary>
    /// Normalizes a renderer identifier, using the Aero composition renderer when no
    /// value has been persisted yet.
    /// </summary>
    public static string NormalizeOrDefault(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? AeroComposition
            : value.Trim().ToLowerInvariant();

    /// <summary>Returns whether a normalized renderer identifier is structurally valid.</summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength)
        {
            return false;
        }

        var normalized = value.AsSpan();
        if (!IsAlphaNumeric(normalized[0]) || !IsAlphaNumeric(normalized[^1]))
        {
            return false;
        }

        var hasNamespaceSeparator = false;
        var previousWasSeparator = false;
        foreach (var character in normalized)
        {
            var isSeparator = character is '.' or '-';
            if (!IsAlphaNumeric(character) && !isSeparator)
            {
                return false;
            }

            if (isSeparator && previousWasSeparator)
            {
                return false;
            }

            hasNamespaceSeparator |= character == '.';
            previousWasSeparator = isSeparator;
        }

        return hasNamespaceSeparator;
    }

    private static bool IsAlphaNumeric(char character)
        => character is >= 'a' and <= 'z' or >= '0' and <= '9';
}

/// <summary>Stable editor surface identifiers advertised by page renderers.</summary>
public static class PageEditorKinds
{
    /// <summary>The visual Aero composition editor.</summary>
    public const string VisualComposition = "visual.composition";

    /// <summary>A full-page source editor.</summary>
    public const string Source = "source";
}

/// <summary>
/// Describes one explicitly registered page-rendering strategy to manager clients.
/// </summary>
/// <param name="Id">The stable persisted renderer identifier.</param>
/// <param name="DisplayName">The user-facing renderer name.</param>
/// <param name="EditorKind">The editor surface required by the renderer.</param>
/// <param name="SupportsFragments">Whether the renderer can be embedded as an Aero fragment.</param>
/// <param name="IsExperimental">Whether the renderer is an experimental feature.</param>
/// <param name="SourceLanguage">The Monaco language for source renderers, or null for visual renderers.</param>
/// <param name="InitialSource">The initial source assigned when a new source page type is selected.</param>
public sealed record PageRendererDescriptor(
    string Id,
    string DisplayName,
    string EditorKind,
    bool SupportsFragments,
    bool IsExperimental,
    string? SourceLanguage = null,
    string? InitialSource = null)
{
    /// <summary>Gets whether this renderer owns immutable page source.</summary>
    public bool RequiresSource =>
        string.Equals(EditorKind, PageEditorKinds.Source, StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(SourceLanguage);
}
