using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Content.Views;

namespace Aero.Cms.Core.Content.Views;

/// <summary>Observes allow-listed sources and generates their canonical scoped statements.</summary>
public sealed class RegisteredContentViewSourceSnapshotService(
    IContentViewSourceRegistry registry,
    IContentSchemaMetadataReader metadata) : IContentViewSourceSnapshotService
{
    public async Task<IReadOnlyList<ContentViewSourceSnapshot>> ListAsync(CancellationToken ct = default)
    {
        if (!registry.IsValid)
            throw new InvalidOperationException($"Content view source registry is invalid: {string.Join("; ", registry.Errors)}");

        var definitions = await metadata.ReadTableDefinitionsAsync(ct);
        return registry.Definitions
            .Select(source => definitions.TryGetValue(source.Table, out var physicalDefinition)
                ? TryCreate(source, physicalDefinition)
                : null)
            .Where(snapshot => snapshot is not null)
            .Cast<ContentViewSourceSnapshot>()
            .ToArray();
    }

    public async Task<ContentViewSourceSnapshot?> GetAsync(string alias, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(alias) || !registry.IsValid || !registry.TryGetByAlias(alias, out var source))
            return null;
        var definitions = await metadata.ReadTableDefinitionsAsync(ct);
        return definitions.TryGetValue(source!.Table, out var physicalDefinition)
            ? TryCreate(source, physicalDefinition)
            : null;
    }

    private static ContentViewSourceSnapshot? TryCreate(ContentViewSourceDefinition source, string physicalDefinition)
    {
        var isMaterializedView = Regex.IsMatch(
            physicalDefinition,
            @"\bAS\s+SELECT\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        if (isMaterializedView != (source.Kind == ContentViewSourceKind.MaterializedView)) return null;
        if (string.IsNullOrWhiteSpace(source.IdentityField)
            || string.IsNullOrWhiteSpace(source.SearchField)
            || !source.TryGetPhysicalField(source.IdentityField, out var identityField)
            || !source.TryGetPhysicalField(source.SearchField, out var searchField))
            return null;

        var predicates = new List<string>
        {
            $"{source.TenantField} = {ReservedContentViewScopeBinder.TenantParameter}",
            $"{source.SiteField} = {ReservedContentViewScopeBinder.SiteParameter}"
        };
        predicates.AddRange((source.RequiredBooleanPredicates ?? [])
            .Select(predicate => $"{predicate.Field} = {predicate.Value.ToString().ToLowerInvariant()}"));
        var scopePredicate = string.Join(" AND ", predicates);
        var outputFields = source.OutputFieldMappings is { Count: > 0 }
            ? string.Join(", ", source.OutputFieldMappings.Keys.OrderBy(field => field, StringComparer.Ordinal))
            : "*";
        var order = source.TryGetPhysicalField(source.TitleField ?? source.IdentityField, out var titleField)
            ? string.Equals(titleField, identityField, StringComparison.Ordinal)
                ? $" ORDER BY {identityField} ASC"
                : $" ORDER BY {titleField} ASC, {identityField} ASC"
            : $" ORDER BY {identityField} ASC";
        var list = $"SELECT {outputFields} FROM {source.Table} WHERE {scopePredicate}{order} LIMIT 50";
        var entry = $"SELECT {outputFields} FROM {source.Table} WHERE {scopePredicate} AND {identityField} = $entryId LIMIT 1";
        var search = $"SELECT {outputFields} FROM {source.Table} WHERE {scopePredicate} AND {searchField} CONTAINS $search{order} LIMIT 50";
        return new ContentViewSourceSnapshot(
            source.Alias,
            source.EffectiveDisplayName,
            source.Description,
            source.Kind,
            source.Table,
            CreateFingerprint(source, physicalDefinition),
            source.SuggestedShapeAlias,
            source.IdentityField,
            source.TitleField,
            list,
            entry,
            search);
    }

    private static string CreateFingerprint(ContentViewSourceDefinition source, string physicalDefinition)
    {
        var mappings = string.Join(',', (source.OutputFieldMappings ?? new Dictionary<string, string>())
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => $"{item.Key}>{item.Value}"));
        var predicates = string.Join(',', (source.RequiredBooleanPredicates ?? [])
            .OrderBy(item => item.Field, StringComparer.Ordinal)
            .Select(item => $"{item.Field}={item.Value.ToString().ToLowerInvariant()}"));
        var canonical = string.Join('|', new[]
        {
            physicalDefinition.Replace("\r\n", "\n", StringComparison.Ordinal).Trim(),
            source.Alias,
            source.Kind.ToString(),
            source.Table,
            source.TenantField,
            source.SiteField,
            source.SuggestedShapeAlias ?? string.Empty,
            source.IdentityField ?? string.Empty,
            source.TitleField ?? string.Empty,
            source.SearchField ?? string.Empty,
            mappings,
            predicates
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..32];
    }
}
