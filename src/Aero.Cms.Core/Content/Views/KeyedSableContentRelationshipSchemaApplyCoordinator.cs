using System.Text.Json;
using Aero.Cms.Abstractions.Content.Views;
using AeroDB.Sable;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Core.Content.Views;

/// <summary>
/// Applies relationship DDL using only a host-specified keyed store. The main unkeyed CMS store
/// is intentionally never resolved here: schema credentials must be separately configured.
/// </summary>
public sealed class KeyedSableContentRelationshipSchemaApplyCoordinator(
    IServiceProvider services,
    object storeKey) : IContentRelationshipSchemaApplyCoordinator
{
    private IDocumentStore? Store => services.GetKeyedService<IDocumentStore>(storeKey);
    public bool IsEnabled => Store is not null;

    public async Task<RelationshipDdlApplyJournal> ApplyAtomicallyAsync(RelationshipDdlPreview preview, ContentSchemaActor actor, CancellationToken ct = default)
    {
        if (!actor.IsValid || Store is null)
            throw new InvalidOperationException("A separately keyed privileged schema store is required.");
        await using var session = await Store.OpenSessionAsync(new SessionOptions(), ct);
        await using var transaction = await session.BeginTransactionAsync(ct);
        try
        {
            if (await ExistsInLiveSchemaAsync(session, preview.Relationship, ct))
                throw new InvalidOperationException("The physical relationship already exists in the live schema and is database-owned.");
            // Semicolon-separated DDL is sent once through the same explicit transaction which
            // subsequently locks the definition and writes the journal.
            await session.RawQueryAsync<JsonElement>(string.Join("\n", preview.Statements), null, ct);
            var relationship = await session.Query<ContentRelationshipDocument>().FirstOrDefaultAsync(item =>
                item.Id == preview.Relationship.Id && item.TenantId == preview.Relationship.Scope.TenantId && item.SiteId == preview.Relationship.Scope.SiteId, ct);
            if (relationship is null
                || relationship.OwnershipState != ContentRelationshipOwnershipState.CmsDraft.ToString()
                || !string.Equals(relationship.SchemaFingerprint, preview.ProposedSchemaFingerprint, StringComparison.Ordinal))
                throw new InvalidOperationException("The relationship changed before its schema application could be committed.");

            relationship.OwnershipState = ContentRelationshipOwnershipState.Applied.ToString();
            session.Store(relationship);
            var journal = new ContentRelationshipDdlJournalDocument
            {
                TenantId = preview.Relationship.Scope.TenantId,
                SiteId = preview.Relationship.Scope.SiteId,
                RelationshipId = preview.Relationship.Id,
                AppliedSchemaFingerprint = preview.ProposedSchemaFingerprint,
                AppliedOn = DateTimeOffset.UtcNow,
                AppliedBy = actor.Subject
            };
            session.Store(journal);
            await session.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new RelationshipDdlApplyJournal(journal.RelationshipId, preview.Relationship.Scope, journal.AppliedSchemaFingerprint, journal.AppliedOn, journal.AppliedBy);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task<bool> ExistsInLiveSchemaAsync(IDocumentSession session, ContentRelationshipDefinition relationship, CancellationToken ct)
    {
        var info = await session.RawQueryAsync<JsonElement>("INFO FOR DB;", null, ct);
        var text = string.Join("\n", info.Select(item => item.GetRawText()));
        return relationship.Kind switch
        {
            ContentRelationshipKind.GraphEdge => text.Contains($"DEFINE TABLE {relationship.EdgeTable ?? relationship.Alias} TYPE RELATION", StringComparison.OrdinalIgnoreCase),
            ContentRelationshipKind.RecordLink or ContentRelationshipKind.SelfHierarchy => !string.IsNullOrWhiteSpace(relationship.SourceField)
                && text.Contains($"DEFINE FIELD {relationship.SourceField} ON TABLE {relationship.SourceTable}", StringComparison.OrdinalIgnoreCase),
            ContentRelationshipKind.AssociationRecord => !string.IsNullOrWhiteSpace(relationship.EdgeTable)
                && !string.IsNullOrWhiteSpace(relationship.SourceField)
                && !string.IsNullOrWhiteSpace(relationship.TargetField)
                && text.Contains($"DEFINE TABLE {relationship.EdgeTable} SCHEMAFULL", StringComparison.OrdinalIgnoreCase)
                && text.Contains($"DEFINE FIELD {relationship.SourceField} ON TABLE {relationship.EdgeTable}", StringComparison.OrdinalIgnoreCase)
                && text.Contains($"DEFINE FIELD {relationship.TargetField} ON TABLE {relationship.EdgeTable}", StringComparison.OrdinalIgnoreCase),
            // Field joins do not claim ownership of a physical schema relation.
            _ => false
        };
    }
}

/// <summary>Signals availability of the separately keyed store without ever resolving an unkeyed store.</summary>
public sealed class KeyedSableContentSchemaCommandExecutor(IServiceProvider services, object storeKey) : IPrivilegedContentSchemaCommandExecutor
{
    public bool IsEnabled => services.GetKeyedService<IDocumentStore>(storeKey) is not null;
    public Task ExecuteAsync(IReadOnlyList<string> statements, CancellationToken ct = default)
        => Task.FromException(new InvalidOperationException("Use the atomic relationship schema apply coordinator; standalone schema commands are not permitted."));
}

public static class ContentRelationshipSchemaServiceCollectionExtensions
{
    /// <summary>
    /// Enables the lifecycle only with the keyed store registered by the consuming host. Hosts
    /// must independently register that key with separate privileged credentials.
    /// </summary>
    public static IServiceCollection AddKeyedContentRelationshipSchemaApply(this IServiceCollection services, object privilegedStoreKey)
    {
        ArgumentNullException.ThrowIfNull(privilegedStoreKey);
        services.AddScoped<IPrivilegedContentSchemaCommandExecutor>(provider => new KeyedSableContentSchemaCommandExecutor(provider, privilegedStoreKey));
        services.AddScoped<IContentRelationshipSchemaApplyCoordinator>(provider => new KeyedSableContentRelationshipSchemaApplyCoordinator(provider, privilegedStoreKey));
        return services;
    }
}
