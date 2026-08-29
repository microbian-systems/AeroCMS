using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content;
using Aero.Cms.Core.Content.Indexing;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Modules.Content.Caching;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Content;

/// <summary>Processes one bounded, durable generation-fenced shared-field projection repair batch.</summary>
public interface IContentTranslationProjectionWorkProcessor
{
    Task<bool> ProcessNextBatchAsync(int maximumItems = 100, CancellationToken cancellationToken = default);
}

internal sealed class ContentTranslationProjectionWorkProcessor(
    IDocumentSession session,
    IContentTypeService contentTypes,
    ContentSearchProjectionService projections,
    ContentCacheInvalidator cacheInvalidator) : IContentTranslationProjectionWorkProcessor
{
    public async Task<bool> ProcessNextBatchAsync(int maximumItems = 100, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var work = await session.Query<ContentTranslationProjectionWorkDocument>()
            .Where(candidate => !candidate.Completed
                && (candidate.NextAttemptOn == null || candidate.NextAttemptOn <= now))
            .OrderBy(candidate => candidate.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (work is null) return false;

        session.UpdateExpectedVersion(work, work.Version);
        work.AttemptCount++;
        work.LastAttemptOn = now;

        var group = await session.LoadAsync<ContentTranslationGroupDocument>(work.TranslationGroupId, cancellationToken);
        if (group is null || group.SiteId != work.SiteId || group.Version != work.GroupStorageVersion || group.Revision != work.GroupRevision)
        {
            work.Completed = true; // Superseded/deleted generations must never overwrite newer projections.
            work.LastFailure = null;
            work.NextAttemptOn = null;
            await session.SaveChangesAsync(cancellationToken);
            return true;
        }

        var type = await contentTypes.GetByAliasAsync(work.SiteId, group.ContentTypeAlias, cancellationToken);
        if (type is not Aero.Core.Railway.Result<ContentTypeDefinition, Aero.Core.AeroError>.Ok typeOk)
        {
            work.LastFailure = "The content type is unavailable.";
            work.NextAttemptOn = RetryAt(now, work.AttemptCount);
            await session.SaveChangesAsync(cancellationToken);
            return true; // Keep durable work pending until its type becomes available.
        }

        var take = Math.Clamp(maximumItems, 1, 100);
        var variants = await session.Query<ContentItem>()
            .Where(item => item.SiteId == work.SiteId
                && item.ContentTypeAlias == group.ContentTypeAlias
                && item.TranslationGroupId == group.Id
                && (work.LastProcessedItemId == null || item.Id > work.LastProcessedItemId))
            .OrderBy(item => item.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
        foreach (var variant in variants)
        {
            await projections.StageUpsertAsync(variant, typeOk.Value, group.SharedFields, cancellationToken);
            work.LastProcessedItemId = variant.Id;
        }
        // Projection rows and durable progress are committed before cache eviction. If the
        // process stops after this commit, the remaining work is replayed safely.
        await session.SaveChangesAsync(cancellationToken);

        // Never mark work complete until cache invalidation has succeeded.  A retry is
        // preferable to serving a completed generation with stale hydrated variants.
        if (!await cacheInvalidator.TryInvalidateTranslationGroupAsync(
                work.SiteId, group.Id, group.ContentTypeAlias, cancellationToken))
        {
            work.LastFailure = "Cache invalidation did not complete.";
            work.NextAttemptOn = RetryAt(now, work.AttemptCount);
            session.UpdateExpectedVersion(work, work.Version);
            await session.SaveChangesAsync(cancellationToken);
            return true;
        }

        work.LastFailure = null;
        work.NextAttemptOn = null;
        if (variants.Count < take) work.Completed = true;
        session.UpdateExpectedVersion(work, work.Version);
        try
        {
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            session.ClearChanges();
        }
        return true;
    }

    private static DateTimeOffset RetryAt(DateTimeOffset now, int attemptCount) =>
        now.AddSeconds(Math.Min(60, Math.Pow(2, Math.Min(attemptCount, 6))));
}
