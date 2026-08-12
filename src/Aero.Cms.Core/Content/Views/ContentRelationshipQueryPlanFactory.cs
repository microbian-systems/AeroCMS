using Aero.Cms.Abstractions.Content.Views;

namespace Aero.Cms.Core.Content.Views;

/// <summary>
/// Emits immutable, scope-complete plan metadata from a locked relationship definition. This is
/// deliberately a factory rather than an editor parser: only an Applied relationship with
/// registered physical sources can produce a public-plan candidate. The public executor remains
/// fail-closed until a host binds this candidate to a transport that can evaluate graph syntax.
/// </summary>
public sealed class ContentRelationshipQueryPlanFactory(
    IContentPhysicalSchemaTargetRegistry targets,
    IContentViewSourceRegistry sources)
{
    public ContentViewTrustedQueryPlanDefinition? Create(ContentRelationshipDefinition relationship,
        ContentViewSourceDefinition root, ContentViewSourceDefinition target)
    {
        if (relationship.OwnershipState != ContentRelationshipOwnershipState.Applied
            || relationship.IsMutationBlocked is false
            || relationship.Kind != ContentRelationshipKind.GraphEdge
            || !targets.TryGet(relationship.SourceShapeAlias ?? string.Empty, relationship.SourceTable, out _)
            || !targets.TryGet(relationship.TargetShapeAlias ?? string.Empty, relationship.TargetTable, out _)
            || !string.Equals(root.Table, relationship.SourceTable, StringComparison.Ordinal)
            || !string.Equals(target.Table, relationship.TargetTable, StringComparison.Ordinal)
            || !sources.TryGetByTable(root.Table, out var registeredRoot)
            || !sources.TryGetByTable(target.Table, out var registeredTarget)
            || !Equals(registeredRoot, root) || !Equals(registeredTarget, target)) return null;

        var planSources = new List<ContentViewSourceDefinition> { root, target };
        var descriptors = new List<ContentViewScopedPlanSource> { new(root, "root"), new(target, "target") };
        var traversal = CreateEdgeTraversal(relationship, planSources, descriptors);
        if (traversal is null) return null;
        var predicates = string.Join(" AND ", descriptors.SelectMany(descriptor =>
        {
            var scope = new[]
            {
                $"{descriptor.Qualifier}.{descriptor.Source.TenantField} = $tenantId",
                $"{descriptor.Qualifier}.{descriptor.Source.SiteField} = $siteId"
            };
            var required = (descriptor.Source.RequiredBooleanPredicates ?? [])
                .OrderBy(predicate => predicate.Field, StringComparer.Ordinal)
                .Select(predicate => $"{descriptor.Qualifier}.{predicate.Field} = {predicate.Value.ToString().ToLowerInvariant()}");
            return scope.Concat(required);
        }));
        var statement = $"SELECT {traversal} FROM {root.Table} AS root WHERE {predicates} LIMIT 100";
        var provisional = new ContentViewTrustedQueryPlanDefinition($"relationship:{relationship.Alias}", statement, root, [target], planSources.Skip(2).ToArray(), descriptors);
        return provisional with { Alias = $"relationship:{relationship.Alias}:{provisional.PlanFingerprint}" };
    }

    private string? CreateEdgeTraversal(ContentRelationshipDefinition relationship, List<ContentViewSourceDefinition> planSources,
        List<ContentViewScopedPlanSource> descriptors)
    {
        if (string.IsNullOrWhiteSpace(relationship.EdgeTable)
            || !targets.TryGetTable(relationship.EdgeTable, out _)
            || !sources.TryGetByTable(relationship.EdgeTable, out var edge)) return null;
        planSources.Add(edge);
        descriptors.Add(new(edge, "edge"));
        return $"root->{relationship.EdgeTable}->target";
    }
}
