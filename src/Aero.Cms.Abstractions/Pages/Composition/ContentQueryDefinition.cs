using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Abstractions.Pages.Composition;

/// <summary>
/// Persists one named, bounded hierarchy query beside a page composition.
/// </summary>
/// <remarks>
/// The stable content-type identifier is authoritative. The alias is retained
/// only for authoring diagnostics.
/// </remarks>
public sealed partial record ContentQueryDefinition
{
    /// <summary>Gets the maximum number of named queries allowed on one page.</summary>
    public const int MaximumQueriesPerPage = 8;

    /// <summary>Gets the maximum length of a normalized query name.</summary>
    public const int MaximumNameLength = 64;

    /// <summary>Gets the maximum number of projected fields.</summary>
    public const int MaximumProjectionFields = 64;

    /// <summary>Gets the hard maximum requested hierarchy depth.</summary>
    public const int MaximumDepthLimit = 16;

    /// <summary>Gets the hard maximum requested item count.</summary>
    public const int MaximumItemLimit = 500;

    /// <summary>Gets the maximum aggregate requested items across one page.</summary>
    public const int MaximumItemsPerPage = 500;

    /// <summary>Gets the normalized script binding name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the authoritative stable content-type identifier.</summary>
    public long ContentTypeId { get; init; }

    /// <summary>Gets the last-known alias retained for diagnostics.</summary>
    public string ContentTypeAlias { get; init; } = string.Empty;

    /// <summary>Gets the bounded traversal to execute.</summary>
    public ContentTraversal Traversal { get; init; } = ContentTraversal.RootsWithDescendants;

    /// <summary>Gets the required root for child, descendant, and ancestor traversals.</summary>
    public long? RootId { get; init; }

    /// <summary>Gets the maximum number of returned parent/child edges.</summary>
    public int MaximumDepth { get; init; } = 4;

    /// <summary>Gets the maximum number of returned nodes.</summary>
    public int MaximumItems { get; init; } = 100;

    /// <summary>Gets the immutable allowlist of content fields exposed to scripts.</summary>
    public ImmutableArray<string> Projection { get; init; } = [];

    /// <summary>Normalizes a query binding name for persistence and lookup.</summary>
    public static string NormalizeName(string? name)
        => (name ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Returns whether a name is already in its canonical script-safe form.</summary>
    public static bool IsValidName(string? name)
        => name is not null && QueryNamePattern().IsMatch(name);

    /// <summary>Creates an independent, normalized immutable snapshot.</summary>
    public ContentQueryDefinition CreateSnapshot() => this with
    {
        Name = NormalizeName(Name),
        ContentTypeAlias = (ContentTypeAlias ?? string.Empty).Trim(),
        Projection = NormalizeProjection(Projection)
    };

    /// <summary>Normalizes and de-duplicates projected field names.</summary>
    public static ImmutableArray<string> NormalizeProjection(IEnumerable<string>? projection)
        => projection is null
            ? []
            : projection
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray();

    /// <summary>
    /// Validates a page's complete declaration set at both persistence and
    /// defensive runtime boundaries.
    /// </summary>
    public static IReadOnlyList<string> ValidateDefinitions(
        IReadOnlyList<ContentQueryDefinition>? definitions)
    {
        var queries = definitions ?? [];
        var errors = new List<string>();
        if (queries.Count > MaximumQueriesPerPage)
        {
            errors.Add(
                $"A page cannot contain more than {MaximumQueriesPerPage} content queries.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalItems = 0;
        foreach (var query in queries)
        {
            if (query is null)
            {
                errors.Add("Content query declarations cannot be null.");
                continue;
            }

            var normalizedName = NormalizeName(query.Name);
            if (!IsValidName(normalizedName)
                || !string.Equals(query.Name, normalizedName, StringComparison.Ordinal))
            {
                errors.Add(
                    $"Content query '{query.Name}' must use a normalized lowercase identifier containing only letters, numbers, and underscores.");
            }
            else if (!names.Add(normalizedName))
            {
                errors.Add(
                    $"Content query name '{normalizedName}' cannot be declared more than once.");
            }

            if (query.ContentTypeId <= 0)
                errors.Add($"Content query '{normalizedName}' must identify a stable content type.");
            if (string.IsNullOrWhiteSpace(query.ContentTypeAlias))
                errors.Add($"Content query '{normalizedName}' must retain the content-type alias.");
            if (!Enum.IsDefined(query.Traversal))
                errors.Add($"Content query '{normalizedName}' has an unsupported traversal.");

            if (query.Traversal is (
                    ContentTraversal.Children
                    or ContentTraversal.Descendants
                    or ContentTraversal.Ancestors)
                && query.RootId is not > 0)
            {
                errors.Add($"Content query '{normalizedName}' requires a stable root item.");
            }

            if (query.Traversal is (
                    ContentTraversal.Roots
                    or ContentTraversal.RootsWithDescendants)
                && query.RootId is not null)
            {
                errors.Add($"Content query '{normalizedName}' cannot specify a root item.");
            }

            if (query.MaximumDepth is < 1 or > MaximumDepthLimit)
            {
                errors.Add(
                    $"Content query '{normalizedName}' depth must be between 1 and {MaximumDepthLimit}.");
            }

            if (query.MaximumItems is < 1 or > MaximumItemLimit)
            {
                errors.Add(
                    $"Content query '{normalizedName}' item count must be between 1 and {MaximumItemLimit}.");
            }

            totalItems += Math.Max(query.MaximumItems, 0);
            var projection = query.Projection.IsDefault ? [] : query.Projection;
            if (projection.Length > MaximumProjectionFields
                || projection.Any(string.IsNullOrWhiteSpace)
                || projection.Distinct(StringComparer.OrdinalIgnoreCase).Count() != projection.Length)
            {
                errors.Add(
                    $"Content query '{normalizedName}' contains an invalid or duplicate field projection.");
            }
        }

        if (totalItems > MaximumItemsPerPage)
        {
            errors.Add(
                $"Content queries cannot request more than {MaximumItemsPerPage} total items per page.");
        }

        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex QueryNamePattern();
}
