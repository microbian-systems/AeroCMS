using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Content.Views;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Views;

/// <summary>
/// Reads SurrealDB's INFO output and exposes existing record links and relation tables as
/// external metadata. It deliberately considers only host-registered tables, so INFO FOR DB
/// cannot leak unrelated database schema into a site's editor.
/// </summary>
public sealed class SableContentRelationshipSchemaDiscovery(
    IContentSchemaMetadataReader reader,
    IContentPhysicalSchemaTargetRegistry targets) : IContentRelationshipSchemaDiscovery
{
    private static readonly Regex Relation = new(@"(?<statement>DEFINE\s+TABLE\s+`?(?<edge>[A-Za-z][A-Za-z0-9_]*)`?\s+(?=[^;]*\bTYPE\s+RELATION\b)(?=[^;]*\bIN\s+`?(?<source>[A-Za-z][A-Za-z0-9_]*)`?)(?=[^;]*\bOUT\s+`?(?<target>[A-Za-z][A-Za-z0-9_]*)`?)[^;]*(?:;|$))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Link = new(@"(?<statement>DEFINE\s+FIELD\s+`?(?<field>[A-Za-z][A-Za-z0-9_]*)`?\s+ON(?:\s+TABLE)?\s+`?(?<source>[A-Za-z][A-Za-z0-9_]*)`?\s+TYPE\s+(?:(?<option>option)<)?(?:(?<array>array)<)?record<`?(?<target>[A-Za-z][A-Za-z0-9_]*)`?>[^;]*(?:;|$))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TableDefinition = new(@"DEFINE\s+TABLE\s+`?(?<table>[A-Za-z][A-Za-z0-9_]*)`?\b[^;]*(?:;|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex FieldDefinition = new(@"DEFINE\s+FIELD\s+`?(?<field>[A-Za-z][A-Za-z0-9_]*)`?\s+ON(?:\s+TABLE)?\s+`?(?<table>[A-Za-z][A-Za-z0-9_]*)`?\s+TYPE\s+(?<definition>[^;]+);", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public async Task<IReadOnlyList<ContentRelationshipDefinition>> DiscoverAsync(ContentViewScope scope, CancellationToken ct = default)
    {
        if (!scope.IsValid) return [];
        var definitions = await reader.ReadTableDefinitionsAsync(ct);
        var found = new List<ContentRelationshipDefinition>();
        var schema = string.Join("\n", definitions.Values);
        var relationMatches = Relation.Matches(schema).Cast<Match>().ToArray();
        var relationTables = relationMatches
            .Select(match => match.Groups["edge"].Value)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var match in relationMatches)
        {
            var source = match.Groups["source"].Value;
            var target = match.Groups["target"].Value;
            var edge = match.Groups["edge"].Value;
            if (!targets.TryGetTable(source, out var sourceTarget)
                || !targets.TryGetTable(target, out var targetTarget)
                || !targets.TryGetTable(edge, out _)
                || !TryGetRequiredScopeDefinitions(schema, edge, out var scopeDefinitions)) continue;
            found.Add(Create(
                scope,
                edge,
                sourceTarget!.ShapeAlias,
                targetTarget!.ShapeAlias,
                source,
                target,
                null,
                null,
                edge,
                ContentRelationshipKind.GraphEdge,
                ContentRelationshipCardinality.ManyToMany,
                [
                    match.Groups["statement"].Value,
                    .. FindFieldDefinitions(schema, edge, "in", "out"),
                    .. scopeDefinitions
                ],
                scopeIsProvable: true));
        }
        foreach (Match match in Link.Matches(schema))
        {
            var source = match.Groups["source"].Value;
            var target = match.Groups["target"].Value;
            // SurrealDB reports native relation endpoints as record-typed in/out fields.
            // They belong to the graph definition and must not become extra direct links.
            if (relationTables.Contains(source)) continue;
            if (!targets.TryGetTable(source, out var sourceTarget)
                || !targets.TryGetTable(target, out var targetTarget)) continue;

            var cardinality = match.Groups["array"].Success
                ? ContentRelationshipCardinality.OneToMany
                : ContentRelationshipCardinality.ManyToOne;
            var kind = string.Equals(source, target, StringComparison.Ordinal)
                && match.Groups["option"].Success
                && !match.Groups["array"].Success
                ? ContentRelationshipKind.SelfHierarchy
                : ContentRelationshipKind.RecordLink;

            // Record references do not constrain the tenant/site of the target record. They remain
            // visible as external metadata, but their observed fingerprint intentionally cannot
            // validate a legacy CMS-applied relationship as same-scope.
            found.Add(Create(
                scope,
                $"{source}_{match.Groups["field"].Value}",
                sourceTarget!.ShapeAlias,
                targetTarget!.ShapeAlias,
                source,
                target,
                match.Groups["field"].Value,
                null,
                null,
                kind,
                cardinality,
                [match.Groups["statement"].Value],
                scopeIsProvable: false));
        }
        foreach (var associationTable in targets.All
                     .Where(target => target.RequiresTenantAndSiteFields))
        {
            if (!TryGetRequiredScopeDefinitions(schema, associationTable.TableName, out var scopeDefinitions)
                || FindTableDefinition(schema, associationTable.TableName) is not { } tableDefinition)
                continue;
            // A TYPE RELATION table has native in/out links, but it is still a graph edge—not
            // an association record. Without this guard the same physical table can be emitted
            // twice with two incompatible semantic kinds.
            if (Regex.IsMatch(
                    tableDefinition,
                    @"\bTYPE\s+RELATION\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                continue;
            var links = Link.Matches(schema)
                .Where(match => string.Equals(match.Groups["source"].Value, associationTable.TableName, StringComparison.Ordinal))
                .Where(match => targets.TryGetTable(match.Groups["target"].Value, out _))
                .ToArray();
            if (links.Length != 2) continue;
            var left = links[0];
            var right = links[1];
            if (!targets.TryGetTable(left.Groups["target"].Value, out var sourceTarget)
                || !targets.TryGetTable(right.Groups["target"].Value, out var targetTarget)) continue;
            found.Add(Create(
                scope,
                associationTable.TableName,
                sourceTarget!.ShapeAlias,
                targetTarget!.ShapeAlias,
                left.Groups["target"].Value,
                right.Groups["target"].Value,
                left.Groups["field"].Value,
                right.Groups["field"].Value,
                associationTable.TableName,
                ContentRelationshipKind.AssociationRecord,
                ContentRelationshipCardinality.ManyToMany,
                [
                    tableDefinition,
                    left.Groups["statement"].Value,
                    right.Groups["statement"].Value,
                    .. scopeDefinitions
                ],
                scopeIsProvable: true));
        }
        var distinct = found.Distinct().ToArray();
        var duplicateAlias = distinct
            .GroupBy(item => item.Alias, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateAlias is not null)
        {
            throw new InvalidOperationException(
                $"Physical schema discovery found more than one relationship candidate with alias '{duplicateAlias.Key}'.");
        }

        return distinct;
    }

    private static bool TryGetRequiredScopeDefinitions(
        string schema,
        string table,
        out IReadOnlyList<string> definitions)
    {
        var fields = FieldDefinition.Matches(schema)
            .Where(match => string.Equals(match.Groups["table"].Value, table, StringComparison.Ordinal)
                && (string.Equals(match.Groups["field"].Value, "tenant_id", StringComparison.Ordinal)
                    || string.Equals(match.Groups["field"].Value, "site_id", StringComparison.Ordinal)))
            .ToArray();

        if (!TryGetRequiredScopeField(fields, "tenant_id", out var tenant)
            || !TryGetRequiredScopeField(fields, "site_id", out var site))
        {
            definitions = [];
            return false;
        }

        definitions = [tenant!, site!];
        return true;
    }

    private static bool TryGetRequiredScopeField(
        IEnumerable<Match> fields,
        string name,
        out string? definition)
    {
        var matches = fields.Where(match => string.Equals(match.Groups["field"].Value, name, StringComparison.Ordinal)).ToArray();
        definition = null;
        if (matches.Length != 1) return false;

        var typeDefinition = Normalize(matches[0].Groups["definition"].Value);
        if (!Regex.IsMatch(
                typeDefinition,
                @"^int(?:\s+ASSERT\s+\$value\s*!=\s*NONE)?(?:\s+PERMISSIONS\s+(?:FULL|NONE))?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return false;

        definition = matches[0].Value;
        return true;
    }

    private static ContentRelationshipDefinition Create(
        ContentViewScope scope,
        string alias,
        string sourceShape,
        string targetShape,
        string source,
        string target,
        string? sourceField,
        string? targetField,
        string? edge,
        ContentRelationshipKind kind,
        ContentRelationshipCardinality cardinality,
        IReadOnlyList<string> physicalDefinitions,
        bool scopeIsProvable)
    {
        var normalized = physicalDefinitions
            .Select(Normalize)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        if (!scopeIsProvable) normalized.Add("UNVERIFIED-SAME-SCOPE-CONSTRAINT");
        var fingerprint = ContentRelationshipDdlLifecycle.CreateFingerprint(normalized);
        var stable = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes($"{scope.TenantId}:{scope.SiteId}:{alias}:{fingerprint}")), 0);
        var id = stable == 0 ? -1 : -Math.Abs(stable == long.MinValue ? long.MaxValue : stable);
        return new ContentRelationshipDefinition(id, scope, alias, sourceShape, targetShape, source, target, sourceField, targetField, edge, kind, cardinality, ContentRelationshipOwnershipState.ExternalDiscovered, fingerprint);
    }

    private static string? FindTableDefinition(string schema, string table)
        => TableDefinition.Matches(schema)
            .FirstOrDefault(match => string.Equals(match.Groups["table"].Value, table, StringComparison.Ordinal))
            ?.Value;

    private static IReadOnlyList<string> FindFieldDefinitions(
        string schema,
        string table,
        params string[] fields)
        => FieldDefinition.Matches(schema)
            .Where(match => string.Equals(match.Groups["table"].Value, table, StringComparison.Ordinal)
                && fields.Contains(match.Groups["field"].Value, StringComparer.Ordinal))
            .OrderBy(match => match.Groups["field"].Value, StringComparer.Ordinal)
            .Select(match => match.Value)
            .ToArray();

    private static string Normalize(string definition)
        => Regex.Replace(definition, @"\s+", " ").Trim().TrimEnd(';');
}

/// <summary>Uses the current Sable query session only for INFO FOR DB metadata.</summary>
public sealed class SableContentSchemaMetadataReader(
    IQuerySession session,
    IContentPhysicalSchemaTargetRegistry? targets = null) : IContentSchemaMetadataReader
{
    private static readonly Regex Identifier = new(
        "^[A-Za-z][A-Za-z0-9_]{0,62}$",
        RegexOptions.CultureInvariant);

    public async Task<IReadOnlyDictionary<string, string>> ReadTableDefinitionsAsync(CancellationToken ct = default)
    {
        var client = session.DocumentStore?.Client
            ?? throw new InvalidOperationException("Schema discovery requires the document store that owns the query session.");
        var response = await client.RawQuery("INFO FOR DB;", null, ct);
        if (response.HasErrors)
            throw new InvalidOperationException("SurrealDB rejected the schema metadata query.");
        var tableDefinitions = ReadDefinitionMap(response.GetValue<JsonElement>(0), "tables");
        if (targets is null) return tableDefinitions;

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var table in targets.All
                     .Select(target => target.TableName)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!Identifier.IsMatch(table))
                throw new InvalidOperationException("A registered physical schema target has an invalid table identifier.");
            if (!tableDefinitions.TryGetValue(table, out var tableDefinition)) continue;

            var tableResponse = await client.RawQuery($"INFO FOR TABLE `{table}`;", null, ct);
            if (tableResponse.HasErrors)
                throw new InvalidOperationException($"SurrealDB rejected schema metadata inspection for registered table '{table}'.");
            var fields = ReadDefinitionMap(tableResponse.GetValue<JsonElement>(0), "fields")
                .OrderBy(field => field.Key, StringComparer.Ordinal)
                .Select(field => EnsureTerminated(field.Value));
            result[table] = string.Join('\n', [EnsureTerminated(tableDefinition), .. fields]);
        }

        return result;
    }

    private static Dictionary<string, string> ReadDefinitionMap(JsonElement root, string category)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.ValueKind != JsonValueKind.Object) return result;
        if (root.TryGetProperty(category, out var selected) && selected.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in selected.EnumerateObject())
                if (property.Value.ValueKind == JsonValueKind.String)
                    result[property.Name] = property.Value.GetString() ?? string.Empty;
            return result;
        }

        // Older embedded responses can expose the selected map as the root.
        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
                result[property.Name] = property.Value.GetString() ?? string.Empty;
            else if (property.Value.ValueKind == JsonValueKind.Object)
                foreach (var nested in property.Value.EnumerateObject())
                    if (nested.Value.ValueKind == JsonValueKind.String)
                        result[nested.Name] = nested.Value.GetString() ?? string.Empty;
        }
        return result;
    }

    private static string EnsureTerminated(string definition)
        => definition.TrimEnd().EndsWith(';') ? definition.TrimEnd() : $"{definition.TrimEnd()};";
}
