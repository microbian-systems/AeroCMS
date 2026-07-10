namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Represents a record for PaletteSearchDocument.
/// </summary>
public sealed record PaletteSearchDocument(
    string? DisplayName,
    string? Description,
    string? CatalogId,
    string? Kind,
    string? Section,
    IReadOnlyCollection<string>? Keywords = null);

/// <summary>
/// Represents a class for PaletteSearchMatcher.
/// </summary>
public static class PaletteSearchMatcher
{
        /// <summary>
    /// Matches method.
    /// </summary>
public static bool Matches(PaletteSearchDocument document, string? query)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var terms = query.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return terms.All(term => MatchesTerm(document, term));
    }

    private static bool MatchesTerm(PaletteSearchDocument document, string term) =>
        Contains(document.DisplayName, term) ||
        Contains(document.Description, term) ||
        Contains(document.CatalogId, term) ||
        Contains(document.Kind, term) ||
        Contains(document.Section, term) ||
        document.Keywords?.Any(keyword => Contains(keyword, term)) == true;

    private static bool Contains(string? value, string term) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains(term, StringComparison.OrdinalIgnoreCase);
}
