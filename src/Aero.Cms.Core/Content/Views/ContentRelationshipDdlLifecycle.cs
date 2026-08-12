using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Content.Views;

namespace Aero.Cms.Core.Content.Views;

/// <summary>
/// Generates only deterministic schema DDL. It never creates, links, relates, or migrates rows.
/// Hosts opt in by registering a separate privileged schema identity.
/// </summary>
public sealed class ContentRelationshipDdlLifecycle(
    IPrivilegedContentSchemaCommandExecutor schemaExecutor,
    IContentPhysicalSchemaTargetRegistry? targets = null,
    IContentRelationshipSchemaApplyCoordinator? coordinator = null) : IRelationshipDdlLifecycle
{
    private static readonly Regex Identifier = new("^[A-Za-z][A-Za-z0-9_]{0,62}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public Task<RelationshipDdlPreview> PreviewAsync(ContentRelationshipDefinition relationship, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!relationship.Scope.IsValid || relationship.OwnershipState != ContentRelationshipOwnershipState.CmsDraft)
            throw new InvalidOperationException("Only a valid CMS draft relationship can be prepared for schema application.");

        var statements = CreateStatements(relationship);
        var fingerprint = CreateFingerprint(statements);
        // New drafts intentionally start without a fingerprint; the deterministic preview is the
        // value the editor persists before an apply request is ever permitted.
        if (!string.IsNullOrWhiteSpace(relationship.SchemaFingerprint)
            && !string.Equals(relationship.SchemaFingerprint, fingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("The relationship definition drifted from its deterministic schema preview.");
        return Task.FromResult(new RelationshipDdlPreview(relationship, fingerprint, statements));
    }

    public async Task<RelationshipDdlApplyJournal> ApplyAsync(RelationshipDdlPreview preview, CancellationToken ct = default)
        => throw new InvalidOperationException("Schema application requires an authenticated platform schema actor.");

    public async Task<RelationshipDdlApplyJournal> ApplyAsync(RelationshipDdlPreview preview, ContentSchemaActor actor, CancellationToken ct = default)
    {
        if (!actor.IsValid)
            throw new InvalidOperationException("Schema application requires an authenticated platform schema actor.");
        if (!schemaExecutor.IsEnabled || coordinator?.IsEnabled != true)
            throw new InvalidOperationException("Schema DDL is disabled until a privileged executor and atomic apply coordinator are configured.");
        if (preview.Relationship.Kind is ContentRelationshipKind.GraphEdge
            or ContentRelationshipKind.RecordLink
            or ContentRelationshipKind.SelfHierarchy)
        {
            // This is intentionally not host-configurable. A coordinator/executor can provide
            // credentials, but cannot substitute for a verified atomic global-claim, endpoint
            // assertion, schema verification, and two-site convergence implementation.
            throw new InvalidOperationException(DisabledContentRelationshipSchemaCapabilityProvider.Capability.Reason);
        }
        var regenerated = await PreviewAsync(preview.Relationship, ct);
        if (!string.Equals(regenerated.ProposedSchemaFingerprint, preview.ProposedSchemaFingerprint, StringComparison.Ordinal)
            || !regenerated.Statements.SequenceEqual(preview.Statements, StringComparer.Ordinal))
            throw new InvalidOperationException("The reviewed schema preview no longer matches the relationship definition.");

        ValidateRegisteredTargets(regenerated.Relationship);

        return await coordinator.ApplyAtomicallyAsync(regenerated, actor, ct);
    }

    public static IReadOnlyList<string> CreateStatements(ContentRelationshipDefinition relationship)
    {
        var alias = RequireIdentifier(relationship.Alias, nameof(relationship.Alias));
        var source = RequireIdentifier(relationship.SourceTable, nameof(relationship.SourceTable));
        var target = RequireIdentifier(relationship.TargetTable, nameof(relationship.TargetTable));
        var sourceField = relationship.SourceField is null ? null : RequireIdentifier(relationship.SourceField, nameof(relationship.SourceField));
        var targetField = relationship.TargetField is null ? null : RequireIdentifier(relationship.TargetField, nameof(relationship.TargetField));
        var edge = relationship.EdgeTable is null ? alias : RequireIdentifier(relationship.EdgeTable, nameof(relationship.EdgeTable));
        return relationship.Kind switch
        {
            // A field join is metadata-only: an index on one side neither creates nor enforces a
            // relationship.  Hosts may provision indexes independently for their workload.
            ContentRelationshipKind.FieldJoin when sourceField is not null && targetField is not null => [],
            ContentRelationshipKind.FieldJoin => [],
            ContentRelationshipKind.RecordLink when sourceField is not null && relationship.Cardinality is ContentRelationshipCardinality.OneToMany or ContentRelationshipCardinality.ManyToMany => [$"DEFINE FIELD {sourceField} ON TABLE {source} TYPE array<record<{target}>>;"],
            ContentRelationshipKind.RecordLink when sourceField is not null => [$"DEFINE FIELD {sourceField} ON TABLE {source} TYPE record<{target}>;"],
            ContentRelationshipKind.GraphEdge => [
                $"DEFINE TABLE {edge} TYPE RELATION IN {source} OUT {target} SCHEMAFULL;",
                $"DEFINE FIELD tenant_id ON TABLE {edge} TYPE int ASSERT $value != NONE;",
                $"DEFINE FIELD site_id ON TABLE {edge} TYPE int ASSERT $value != NONE;"],
            ContentRelationshipKind.SelfHierarchy when source == target && sourceField is not null => [$"DEFINE FIELD {sourceField} ON TABLE {source} TYPE option<record<{source}>>;"],
            ContentRelationshipKind.SelfHierarchy => throw new InvalidOperationException("A self hierarchy must reference the same source and target shape."),
            _ => throw new InvalidOperationException("Unsupported relationship kind.")
        };
    }

    internal static string CreateFingerprint(IReadOnlyList<string> statements)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", statements))))[..32];

    private static string RequireIdentifier(string value, string name)
        => Identifier.IsMatch(value) ? value : throw new InvalidOperationException($"{name} must be a simple SurrealDB identifier.");

    private void ValidateRegisteredTargets(ContentRelationshipDefinition relationship)
    {
        if (targets is null
            || !targets.TryGet(relationship.SourceShapeAlias ?? string.Empty, relationship.SourceTable, out var source)
            || !targets.TryGet(relationship.TargetShapeAlias ?? string.Empty, relationship.TargetTable, out var target)
            || !source.RequiresTenantAndSiteFields
            || !target.RequiresTenantAndSiteFields)
            throw new InvalidOperationException("Physical schema application requires registered source and target tables with tenant/site invariants.");

        if (relationship.Kind == ContentRelationshipKind.GraphEdge
            && (string.IsNullOrWhiteSpace(relationship.EdgeTable)
                || !targets.TryGetTable(relationship.EdgeTable, out var edge)
                || !edge.RequiresTenantAndSiteFields))
            throw new InvalidOperationException("Graph schema application requires a registered edge table with tenant/site invariants.");
    }
}
