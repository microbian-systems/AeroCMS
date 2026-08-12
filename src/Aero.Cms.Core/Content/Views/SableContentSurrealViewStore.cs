using Aero.Cms.Abstractions.Content.Views;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Views;

/// <summary>Sable persistence for append-only drafts and immutable published revisions.</summary>
public sealed class SableContentSurrealViewStore(IDocumentSession session) : IContentSurrealViewStore
{
    internal const int MaximumDraftAllocationAttempts = 4;
    // Bounded single gate (not keyed) protects embedded runtime sessions during a local process.
    // Cross-process collisions are still governed by the database unique revision index.
    private static readonly SemaphoreSlim InProcessAllocationGate = new(1, 1);
    public async Task<ContentSurrealViewRevision?> LoadAsync(ContentViewScope scope, string alias, ContentViewPublicationState state, CancellationToken ct = default)
    {
        if (!scope.IsValid || string.IsNullOrWhiteSpace(alias)) return null;
        var published = state == ContentViewPublicationState.Published;
        var document = await session.Query<ContentSurrealViewDocument>()
            .Where(x => x.TenantId == scope.TenantId && x.SiteId == scope.SiteId && x.Alias == alias && x.IsPublished == published)
            .OrderByDescending(x => x.Version).ThenByDescending(x => x.CreatedOn).FirstOrDefaultAsync(ct);
        return document is null ? null : Map(document);
    }

    public async Task<IReadOnlyList<ContentSurrealViewRevision>> ListPublishedAsync(ContentViewScope scope, CancellationToken ct = default)
    {
        if (!scope.IsValid) return [];
        return (await session.Query<ContentSurrealViewDocument>()
            .Where(x => x.TenantId == scope.TenantId && x.SiteId == scope.SiteId && x.IsPublished)
            .ToListAsync(ct)).GroupBy(x => x.Alias, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(x => x.Version).ThenByDescending(x => x.CreatedOn).First())
            .Select(Map).ToArray();
    }

    public async Task<ContentSurrealViewRevision> SaveDraftAsync(ContentSurrealViewRevision draft, CancellationToken ct = default)
    {
        if (!draft.Scope.IsValid || string.IsNullOrWhiteSpace(draft.Alias))
            throw new ArgumentException("A valid scope and alias are required to save a content-view draft.", nameof(draft));

        // The configured composite unique index is the cross-process compare-and-set boundary.
        // The one bounded local gate prevents the embedded client from corrupting concurrent
        // responses; it never identifies an alias and therefore cannot grow with user input.
        await InProcessAllocationGate.WaitAsync(ct);
        try
        {
            for (var attempt = 0; attempt < MaximumDraftAllocationAttempts; attempt++)
            {
                // Materialize the document rather than a scalar projection. The embedded
                // SurrealDB 3.0.5 response for a non-empty scalar projection is encoded as a
                // map and cannot be decoded by Sable's scalar FirstOrDefault path.
                var latest = await session.Query<ContentSurrealViewDocument>()
                    .Where(x => x.TenantId == draft.Scope.TenantId && x.SiteId == draft.Scope.SiteId && x.Alias == draft.Alias)
                    .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
                var next = latest?.Version ?? 0;
                var document = Map(draft with { Id = 0, Version = next + 1, PublicationState = ContentViewPublicationState.Draft, CreatedOn = DateTimeOffset.UtcNow });
                session.Store(document);
                try { await session.SaveChangesAsync(ct); return Map(document); }
                catch (Exception exception) when (IsUniqueConstraintConflict(exception) && attempt + 1 < MaximumDraftAllocationAttempts) { session.ClearChanges(); }
            }
            throw new InvalidOperationException($"Could not allocate an immutable draft revision for '{draft.Alias}' after {MaximumDraftAllocationAttempts} database conflicts.");
        }
        finally { InProcessAllocationGate.Release(); }
    }

    public async Task<ContentSurrealViewRevision?> PublishAsync(ContentViewScope scope, string alias, long draftVersion, CancellationToken ct = default)
    {
        if (!scope.IsValid || string.IsNullOrWhiteSpace(alias) || draftVersion <= 0) return null;
        var existing = await session.Query<ContentSurrealViewDocument>().FirstOrDefaultAsync(x =>
            x.TenantId == scope.TenantId && x.SiteId == scope.SiteId && x.Alias == alias && x.IsPublished && x.Version == draftVersion, ct);
        if (existing is not null) return Map(existing);
        var draft = await session.Query<ContentSurrealViewDocument>()
            .FirstOrDefaultAsync(x => x.TenantId == scope.TenantId && x.SiteId == scope.SiteId && x.Alias == alias && !x.IsPublished && x.Version == draftVersion, ct);
        if (draft is null) return null;
        var published = new ContentSurrealViewDocument
        {
            TenantId = draft.TenantId, SiteId = draft.SiteId, Alias = draft.Alias, ShapeAlias = draft.ShapeAlias,
            ShapeFingerprint = draft.ShapeFingerprint, SelectStatement = draft.SelectStatement, IdentityField = draft.IdentityField, TitleField = draft.TitleField, Version = draft.Version,
            IsPublished = true, CreatedOn = DateTimeOffset.UtcNow, CreatedBy = draft.CreatedBy,
            EntrySelectStatement = draft.EntrySelectStatement, SearchSelectStatement = draft.SearchSelectStatement
            , CacheEnabled = draft.CacheEnabled, CacheDurationSeconds = draft.CacheDurationSeconds, CacheGeneration = draft.CacheGeneration,
            RelationshipId = draft.RelationshipId, RelationshipSchemaFingerprint = draft.RelationshipSchemaFingerprint,
            PublicExecutionEligible = draft.PublicExecutionEligible, PublicExecutionIneligibilityReason = draft.PublicExecutionIneligibilityReason,
            PublicPlanAlias = draft.PublicPlanAlias, PublicPlanFingerprint = draft.PublicPlanFingerprint,
            PublicPlanDialectFingerprint = draft.PublicPlanDialectFingerprint
        };
        try
        {
            session.Store(published);
            await session.SaveChangesAsync(ct);
            return Map(published);
        }
        catch (Exception exception) when (IsUniqueConstraintConflict(exception))
        {
            // A second process can win the database unique-index race.  Return only the exact
            // immutable revision; any other persistence failure remains visible to the caller.
            var concurrent = await session.Query<ContentSurrealViewDocument>().FirstOrDefaultAsync(x =>
                x.TenantId == scope.TenantId && x.SiteId == scope.SiteId && x.Alias == alias && x.IsPublished && x.Version == draftVersion, ct);
            if (concurrent is not null) return Map(concurrent);
            throw;
        }
    }

    private static ContentSurrealViewRevision Map(ContentSurrealViewDocument source) => new(source.Id,
        new ContentViewScope(source.TenantId, source.SiteId), source.Alias, source.ShapeAlias, source.ShapeFingerprint,
        source.SelectStatement, source.IdentityField, source.TitleField, source.Version, source.IsPublished ? ContentViewPublicationState.Published : ContentViewPublicationState.Draft,
        source.CreatedOn, source.CreatedBy, source.CacheEnabled, TimeSpan.FromSeconds(source.CacheDurationSeconds), source.CacheGeneration,
        source.EntrySelectStatement, source.SearchSelectStatement, source.RelationshipId, source.RelationshipSchemaFingerprint,
        source.PublicExecutionEligible, source.PublicExecutionIneligibilityReason,
        source.PublicPlanAlias, source.PublicPlanFingerprint, source.PublicPlanDialectFingerprint);
    private static ContentSurrealViewDocument Map(ContentSurrealViewRevision source) => new()
    {
        // SableDocument creates a snowflake identity.  Do not overwrite it with the revision
        // sentinel (0), or concurrent drafts target the same physical record before the unique
        // revision index can arbitrate the allocation race.
        Id = source.Id > 0 ? source.Id : AeroDB.Sable.SnowflakeGenerator.NewId(),
        TenantId = source.Scope.TenantId, SiteId = source.Scope.SiteId, Alias = source.Alias,
        ShapeAlias = source.ShapeAlias, ShapeFingerprint = source.ShapeFingerprint, SelectStatement = source.SelectStatement, IdentityField = source.IdentityField, TitleField = source.TitleField,
        Version = source.Version, IsPublished = source.IsPublished, CreatedOn = source.CreatedOn, CreatedBy = source.CreatedBy,
        CacheEnabled = source.CacheEnabled, CacheDurationSeconds = (long)(source.CacheDuration ?? TimeSpan.FromMinutes(5)).TotalSeconds,
        CacheGeneration = source.CacheGeneration, EntrySelectStatement = source.EntrySelectStatement,
        SearchSelectStatement = source.SearchSelectStatement, RelationshipId = source.RelationshipId,
        RelationshipSchemaFingerprint = source.RelationshipSchemaFingerprint,
        PublicExecutionEligible = source.PublicExecutionEligible,
        PublicExecutionIneligibilityReason = source.PublicExecutionIneligibilityReason,
        PublicPlanAlias = source.PublicPlanAlias, PublicPlanFingerprint = source.PublicPlanFingerprint,
        PublicPlanDialectFingerprint = source.PublicPlanDialectFingerprint
    };

    private static bool IsUniqueConstraintConflict(Exception exception)
        => exception.ToString().Contains("unique", StringComparison.OrdinalIgnoreCase)
           || exception.ToString().Contains("already exists", StringComparison.OrdinalIgnoreCase)
           || exception.ToString().Contains("already contains", StringComparison.OrdinalIgnoreCase);
}
