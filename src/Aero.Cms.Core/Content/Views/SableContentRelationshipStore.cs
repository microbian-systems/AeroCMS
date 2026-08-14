using Aero.Cms.Abstractions.Content.Views;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Views;

/// <summary>Durable relationship definitions and DDL journal; does not create row links or graph edges.</summary>
public sealed class SableContentRelationshipStore(IDocumentSession session) : IContentRelationshipStore
{
    public async Task<ContentRelationshipDefinition?> LoadAsync(ContentViewScope scope, string alias, CancellationToken ct = default)
        => (await session.Query<ContentRelationshipDocument>().FirstOrDefaultAsync(x => x.TenantId == scope.TenantId && x.SiteId == scope.SiteId && x.Alias == alias, ct)) is { } document ? Map(document) : null;
    public async Task<IReadOnlyList<ContentRelationshipDefinition>> ListAsync(ContentViewScope scope, CancellationToken ct = default)
        => (await session.Query<ContentRelationshipDocument>().Where(x => x.TenantId == scope.TenantId && x.SiteId == scope.SiteId).ToListAsync(ct)).Select(Map).ToArray();
    public async Task<ContentRelationshipDefinition> SaveDraftAsync(ContentRelationshipDefinition relationship, CancellationToken ct = default)
    {
        if (!relationship.Scope.IsValid || relationship.OwnershipState != ContentRelationshipOwnershipState.CmsDraft) throw new InvalidOperationException("Only CMS draft relationship definitions are editable.");
        var document = new ContentRelationshipDocument { Id = relationship.Id > 0 ? relationship.Id : SnowflakeGenerator.NewId(), TenantId = relationship.Scope.TenantId, SiteId = relationship.Scope.SiteId, Alias = relationship.Alias, SourceShapeAlias = relationship.SourceShapeAlias, TargetShapeAlias = relationship.TargetShapeAlias, SourceTable = relationship.SourceTable, TargetTable = relationship.TargetTable, SourceField = relationship.SourceField, TargetField = relationship.TargetField, EdgeTable = relationship.EdgeTable, Kind = relationship.Kind.ToString(), Cardinality = relationship.Cardinality.ToString(), OwnershipState = relationship.OwnershipState.ToString(), SchemaFingerprint = relationship.SchemaFingerprint };
        session.Store(document); await session.SaveChangesAsync(ct); return Map(document);
    }
    public async Task<ContentRelationshipDefinition> AdoptAsync(ContentRelationshipDefinition relationship, CancellationToken ct = default)
    {
        if (!relationship.Scope.IsValid
            || relationship.OwnershipState != ContentRelationshipOwnershipState.ExternalDiscovered
            || relationship.Kind is not (ContentRelationshipKind.GraphEdge or ContentRelationshipKind.AssociationRecord)
            || string.IsNullOrWhiteSpace(relationship.SchemaFingerprint))
            throw new InvalidOperationException("Only a complete, scope-provable physical graph or association relationship can be adopted.");

        var existing = await session.Query<ContentRelationshipDocument>().FirstOrDefaultAsync(candidate =>
            candidate.TenantId == relationship.Scope.TenantId
            && candidate.SiteId == relationship.Scope.SiteId
            && candidate.Alias == relationship.Alias, ct);
        if (existing is not null)
        {
            var mapped = Map(existing);
            if (mapped.OwnershipState == ContentRelationshipOwnershipState.Adopted
                && SameDescriptor(mapped, relationship))
                return mapped;
            throw new InvalidOperationException("A different managed relationship already uses this alias in the selected site.");
        }

        var document = new ContentRelationshipDocument
        {
            Id = SnowflakeGenerator.NewId(),
            TenantId = relationship.Scope.TenantId,
            SiteId = relationship.Scope.SiteId,
            Alias = relationship.Alias,
            SourceShapeAlias = relationship.SourceShapeAlias,
            TargetShapeAlias = relationship.TargetShapeAlias,
            SourceTable = relationship.SourceTable,
            TargetTable = relationship.TargetTable,
            SourceField = relationship.SourceField,
            TargetField = relationship.TargetField,
            EdgeTable = relationship.EdgeTable,
            Kind = relationship.Kind.ToString(),
            Cardinality = relationship.Cardinality.ToString(),
            OwnershipState = ContentRelationshipOwnershipState.Adopted.ToString(),
            SchemaFingerprint = relationship.SchemaFingerprint
        };
        session.Store(document);
        await session.SaveChangesAsync(ct);
        return Map(document);
    }
    public async Task<RelationshipDdlApplyJournal> SaveAppliedAsync(RelationshipDdlApplyJournal journal, CancellationToken ct = default)
    {
        if (!journal.Scope.IsValid || journal.RelationshipId <= 0 || string.IsNullOrWhiteSpace(journal.AppliedSchemaFingerprint))
            throw new InvalidOperationException("A complete site-scoped relationship journal is required.");
        var relationship = await session.Query<ContentRelationshipDocument>().FirstOrDefaultAsync(x =>
            x.Id == journal.RelationshipId && x.TenantId == journal.Scope.TenantId && x.SiteId == journal.Scope.SiteId, ct);
        if (relationship is null) throw new InvalidOperationException("The relationship no longer exists in this site.");
        if (relationship.OwnershipState == ContentRelationshipOwnershipState.Applied.ToString())
        {
            if (!string.Equals(relationship.SchemaFingerprint, journal.AppliedSchemaFingerprint, StringComparison.Ordinal))
                throw new InvalidOperationException("The applied relationship fingerprint drifted; schema mutation is blocked.");
            return journal;
        }
        if (relationship.OwnershipState != ContentRelationshipOwnershipState.CmsDraft.ToString()
            || !string.Equals(relationship.SchemaFingerprint, journal.AppliedSchemaFingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("The relationship changed or drifted before its schema application could be journaled.");

        relationship.OwnershipState = ContentRelationshipOwnershipState.Applied.ToString();
        var existing = await session.Query<ContentRelationshipDdlJournalDocument>().FirstOrDefaultAsync(x =>
            x.TenantId == journal.Scope.TenantId && x.SiteId == journal.Scope.SiteId
            && x.RelationshipId == journal.RelationshipId && x.AppliedSchemaFingerprint == journal.AppliedSchemaFingerprint, ct);
        if (existing is null)
            session.Store(new ContentRelationshipDdlJournalDocument { TenantId = journal.Scope.TenantId, SiteId = journal.Scope.SiteId, RelationshipId = journal.RelationshipId, AppliedSchemaFingerprint = journal.AppliedSchemaFingerprint, AppliedOn = journal.AppliedOn, AppliedBy = journal.AppliedBy });
        session.Store(relationship);
        await session.SaveChangesAsync(ct);
        return journal;
    }
    public async Task MarkDriftedAsync(ContentViewScope scope, long relationshipId, string observedSchemaFingerprint, CancellationToken ct = default)
    {
        if (!scope.IsValid || relationshipId <= 0 || string.IsNullOrWhiteSpace(observedSchemaFingerprint))
            throw new InvalidOperationException("A complete site-scoped observed schema fingerprint is required.");
        var relationship = await session.Query<ContentRelationshipDocument>().FirstOrDefaultAsync(x =>
            x.Id == relationshipId && x.TenantId == scope.TenantId && x.SiteId == scope.SiteId, ct);
        if (relationship is null) return;
        if ((relationship.OwnershipState is nameof(ContentRelationshipOwnershipState.Applied)
                or nameof(ContentRelationshipOwnershipState.Adopted))
            && !string.Equals(relationship.SchemaFingerprint, observedSchemaFingerprint, StringComparison.Ordinal))
        {
            relationship.OwnershipState = ContentRelationshipOwnershipState.Drifted.ToString();
            session.Store(relationship);
            await session.SaveChangesAsync(ct);
        }
    }
    private static bool SameDescriptor(ContentRelationshipDefinition left, ContentRelationshipDefinition right)
        => left.Scope == right.Scope
            && string.Equals(left.Alias, right.Alias, StringComparison.Ordinal)
            && string.Equals(left.SourceShapeAlias, right.SourceShapeAlias, StringComparison.Ordinal)
            && string.Equals(left.TargetShapeAlias, right.TargetShapeAlias, StringComparison.Ordinal)
            && string.Equals(left.SourceTable, right.SourceTable, StringComparison.Ordinal)
            && string.Equals(left.TargetTable, right.TargetTable, StringComparison.Ordinal)
            && string.Equals(left.SourceField, right.SourceField, StringComparison.Ordinal)
            && string.Equals(left.TargetField, right.TargetField, StringComparison.Ordinal)
            && string.Equals(left.EdgeTable, right.EdgeTable, StringComparison.Ordinal)
            && left.Kind == right.Kind
            && left.Cardinality == right.Cardinality
            && string.Equals(left.SchemaFingerprint, right.SchemaFingerprint, StringComparison.Ordinal);
    private static ContentRelationshipDefinition Map(ContentRelationshipDocument source) => new(source.Id, new(source.TenantId, source.SiteId), source.Alias, source.SourceShapeAlias, source.TargetShapeAlias, source.SourceTable, source.TargetTable, source.SourceField, source.TargetField, source.EdgeTable, Enum.Parse<ContentRelationshipKind>(source.Kind), Enum.Parse<ContentRelationshipCardinality>(source.Cardinality), Enum.Parse<ContentRelationshipOwnershipState>(source.OwnershipState), source.SchemaFingerprint);
}
