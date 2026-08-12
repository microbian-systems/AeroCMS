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
    private static readonly Regex Relation = new(@"DEFINE\s+TABLE\s+(?<edge>[A-Za-z][A-Za-z0-9_]*)\s+TYPE\s+RELATION\s+IN\s+(?<source>[A-Za-z][A-Za-z0-9_]*)\s+OUT\s+(?<target>[A-Za-z][A-Za-z0-9_]*)\s+SCHEMAFULL\s*;", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Link = new(@"DEFINE\s+FIELD\s+(?<field>[A-Za-z][A-Za-z0-9_]*)\s+ON\s+TABLE\s+(?<source>[A-Za-z][A-Za-z0-9_]*)\s+TYPE\s+(?:(?<option>option)<)?(?:(?<array>array)<)?record<(?<target>[A-Za-z][A-Za-z0-9_]*)>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex FieldDefinition = new(@"DEFINE\s+FIELD\s+(?<field>[A-Za-z][A-Za-z0-9_]*)\s+ON\s+TABLE\s+(?<table>[A-Za-z][A-Za-z0-9_]*)\s+TYPE\s+(?<definition>[^;]+);", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public async Task<IReadOnlyList<ContentRelationshipDefinition>> DiscoverAsync(ContentViewScope scope, CancellationToken ct = default)
    {
        if (!scope.IsValid) return [];
        var definitions = await reader.ReadTableDefinitionsAsync(ct);
        var found = new List<ContentRelationshipDefinition>();
        var schema = string.Join("\n", definitions.Values);
        foreach (Match match in Relation.Matches(schema))
        {
            var source = match.Groups["source"].Value;
            var target = match.Groups["target"].Value;
            var edge = match.Groups["edge"].Value;
            if (!targets.TryGetTable(source, out var sourceTarget)
                || !targets.TryGetTable(target, out var targetTarget)
                || !targets.TryGetTable(edge, out _)
                || !HasRequiredScopeFields(schema, edge)) continue;
            found.Add(Create(scope, edge, sourceTarget.ShapeAlias, targetTarget.ShapeAlias, source, target, null, null, edge, ContentRelationshipKind.GraphEdge, ContentRelationshipCardinality.ManyToMany, scopeIsProvable: true));
        }
        foreach (Match match in Link.Matches(schema))
        {
            var source = match.Groups["source"].Value;
            var target = match.Groups["target"].Value;
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
            found.Add(Create(scope, $"{source}_{match.Groups["field"].Value}", sourceTarget.ShapeAlias, targetTarget.ShapeAlias, source, target, match.Groups["field"].Value, null, null, kind, cardinality, scopeIsProvable: false));
        }
        return found.DistinctBy(item => item.Alias, StringComparer.Ordinal).ToArray();
    }

    private static bool HasRequiredScopeFields(string schema, string edgeTable)
    {
        var fields = FieldDefinition.Matches(schema)
            .Where(match => string.Equals(match.Groups["table"].Value, edgeTable, StringComparison.Ordinal)
                && (string.Equals(match.Groups["field"].Value, "tenant_id", StringComparison.Ordinal)
                    || string.Equals(match.Groups["field"].Value, "site_id", StringComparison.Ordinal)))
            .ToArray();

        return HasRequiredScopeField(fields, "tenant_id") && HasRequiredScopeField(fields, "site_id");
    }

    private static bool HasRequiredScopeField(IEnumerable<Match> fields, string name)
    {
        var matches = fields.Where(match => string.Equals(match.Groups["field"].Value, name, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1
            && string.Equals(
                Regex.Replace(matches[0].Groups["definition"].Value, @"\s+", " ").Trim(),
                "int ASSERT $value != NONE",
                StringComparison.OrdinalIgnoreCase);
    }

    private static ContentRelationshipDefinition Create(ContentViewScope scope, string alias, string sourceShape, string targetShape, string source, string target, string? sourceField, string? targetField, string? edge, ContentRelationshipKind kind, ContentRelationshipCardinality cardinality, bool scopeIsProvable)
    {
        var provisional = new ContentRelationshipDefinition(0, scope, alias, sourceShape, targetShape, source, target, sourceField, targetField, edge, kind, cardinality, ContentRelationshipOwnershipState.ExternalDiscovered, string.Empty);
        // Canonicalize against the lifecycle's own deterministic DDL generation so INFO's map/list
        // presentation details do not falsely drift an applied definition.
        var statements = ContentRelationshipDdlLifecycle.CreateStatements(provisional);
        var fingerprint = ContentRelationshipDdlLifecycle.CreateFingerprint(scopeIsProvable
            ? statements
            : statements.Append("UNVERIFIED-SAME-SCOPE-CONSTRAINT").ToArray());
        var stable = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes($"{scope.TenantId}:{scope.SiteId}:{alias}:{fingerprint}")), 0);
        return new ContentRelationshipDefinition(-Math.Abs(stable == long.MinValue ? long.MaxValue : stable), scope, alias, sourceShape, targetShape, source, target, sourceField, targetField, edge, kind, cardinality, ContentRelationshipOwnershipState.ExternalDiscovered, fingerprint);
    }
}

/// <summary>Uses the current Sable query session only for INFO FOR DB metadata.</summary>
public sealed class SableContentSchemaMetadataReader(IQuerySession session) : IContentSchemaMetadataReader
{
    public async Task<IReadOnlyDictionary<string, string>> ReadTableDefinitionsAsync(CancellationToken ct = default)
    {
        var client = session.DocumentStore?.Client
            ?? throw new InvalidOperationException("Schema discovery requires the document store that owns the query session.");
        var response = await client.RawQuery("INFO FOR DB;", null, ct);
        if (response.HasErrors)
            throw new InvalidOperationException("SurrealDB rejected the schema metadata query.");
        var root = response.GetValue<JsonElement>(0);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.ValueKind != JsonValueKind.Object) return result;
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
}
