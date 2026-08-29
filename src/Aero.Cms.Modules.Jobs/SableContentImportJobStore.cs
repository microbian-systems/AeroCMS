using Aero.Cms.Abstractions.Content.Importing;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Jobs;

/// <summary>Sable-backed durable import jobs. Every lease mutation requires the current fencing token.</summary>
internal sealed class SableContentImportJobStore(IDocumentStore store) : IContentImportJobStore
{
    private const int MaximumAttempts = 8;
    private const int MaximumCheckpointLength = 4_096;
    private const int MaximumErrorLength = 4_096;
    private const int MaximumRunnableTake = 100;
    private static readonly TimeSpan MaximumLeaseDuration = TimeSpan.FromMinutes(10);
    public async Task<ContentImportJob?> LoadAsync(long jobId, CancellationToken ct = default)
    {
        await using var session = await store.OpenSessionAsync(new SessionOptions(), ct);
        return jobId > 0 && await session.LoadAsync<ContentImportJobDocument>(jobId, ct) is { } document ? Map(document) : null;
    }

    public async Task<ContentImportJob?> EnsureAsync(ContentImportRequest request, long tenantId, CancellationToken ct = default)
    {
        if (!request.IsValid || tenantId <= 0) return null;
        await using var session = await store.OpenSessionAsync(new SessionOptions(), ct);
        var existing = await session.Query<ContentImportJobDocument>().FirstOrDefaultAsync(x => x.RequestIdentity == request.Identity, ct);
        if (existing is not null) return Map(existing);
        var document = New(request, tenantId);
        session.Store(document);
        try { await session.SaveChangesAsync(ct); return Map(document); }
        catch (Exception exception) when (IsUniqueConflict(exception))
        {
            session.ClearChanges();
            return (await session.Query<ContentImportJobDocument>().FirstOrDefaultAsync(x => x.RequestIdentity == request.Identity, ct)) is { } concurrent ? Map(concurrent) : null;
        }
    }

    public async Task<ContentImportLease?> TryClaimAsync(long jobId, string owner, DateTimeOffset now, TimeSpan duration, CancellationToken ct = default)
    {
        if (jobId <= 0 || !IsBounded(owner, 256) || duration <= TimeSpan.Zero || duration > MaximumLeaseDuration) return null;
        await using var session = await store.OpenSessionAsync(new SessionOptions(), ct);
        var document = await session.LoadAsync<ContentImportJobDocument>(jobId, ct);
        if (document is null || document.State is "Completed" or "Failed" or "ManualReview") return null;
        if (document.Attempt >= MaximumAttempts)
        {
            document.State = "ManualReview"; document.LeaseToken = null; document.LeaseExpiresOn = null; document.NextAttemptOn = null; document.ModifiedOn = now;
            session.Store(document);
            try { await session.SaveChangesAsync(ct); } catch (ConcurrencyException) { session.ClearChanges(); }
            return null;
        }
        var claimable = (document.State == "Pending"
                && (document.NextAttemptOn is null || document.NextAttemptOn <= now))
            || (document.State == "Running"
                && document.LeaseExpiresOn is { } existingExpiry
                && existingExpiry <= now);
        if (!claimable) return null;
        document.State = "Running"; document.Attempt++; document.LeaseToken = Guid.NewGuid().ToString("N");
        document.FencingVersion++; document.LeaseExpiresOn = now.Add(duration); document.NextAttemptOn = null; document.ModifiedOn = now;
        session.Store(document);
        try { await session.SaveChangesAsync(ct); return new(document.Id, document.LeaseToken, document.FencingVersion, document.LeaseExpiresOn.Value); }
        catch (ConcurrencyException) { session.ClearChanges(); return null; }
    }

    public Task<bool> RenewAsync(ContentImportLease lease, DateTimeOffset now, TimeSpan duration, CancellationToken ct = default)
        => duration <= TimeSpan.Zero || duration > MaximumLeaseDuration
            ? Task.FromResult(false)
            : MutateLeaseAsync(lease, document => { document.LeaseExpiresOn = now.Add(duration); return true; }, ct);
    public Task<bool> ReportAsync(ContentImportLease lease, string? checkpoint, long current, long? total, CancellationToken ct = default)
        => IsValidProgress(checkpoint, current, total) ? MutateLeaseAsync(lease, document =>
        {
            if (current < document.ProgressCurrent || (document.ProgressTotal is { } previousTotal && (total is null || total < previousTotal)) || (total is { } suppliedTotal && suppliedTotal < current)) return false;
            document.Checkpoint = checkpoint; document.ProgressCurrent = current; document.ProgressTotal = total; return true;
        }, ct) : Task.FromResult(false);
    public Task<bool> CompleteAsync(ContentImportLease lease, CancellationToken ct = default)
        => MutateLeaseAsync(lease, document => { document.State = "Completed"; document.LeaseToken = null; document.LeaseExpiresOn = null; document.LastError = null; return true; }, ct);
    public Task<bool> RetryAsync(ContentImportLease lease, string? checkpoint, long? progressCurrent, long? progressTotal, string error, CancellationToken ct = default)
        => IsValidFailure(checkpoint, progressCurrent, progressTotal, error) ? MutateLeaseAsync(lease, document =>
        {
            if (!ApplyOptionalProgress(document, checkpoint, progressCurrent, progressTotal)) return false;
            document.LastError = error; document.LeaseToken = null; document.LeaseExpiresOn = null;
            if (document.Attempt >= MaximumAttempts) { document.State = "ManualReview"; document.NextAttemptOn = null; }
            else { document.State = "Pending"; document.NextAttemptOn = DateTimeOffset.UtcNow.Add(Backoff(document.Attempt)); }
            return true;
        }, ct) : Task.FromResult(false);
    public Task<bool> FailAsync(ContentImportLease lease, string? checkpoint, long? progressCurrent, long? progressTotal, string error, CancellationToken ct = default)
        => IsValidFailure(checkpoint, progressCurrent, progressTotal, error) ? MutateLeaseAsync(lease, document =>
        {
            if (!ApplyOptionalProgress(document, checkpoint, progressCurrent, progressTotal)) return false;
            document.State = "Failed"; document.LastError = error; document.LeaseToken = null; document.LeaseExpiresOn = null; document.NextAttemptOn = null; return true;
        }, ct) : Task.FromResult(false);
    public Task<bool> ReleaseAsync(ContentImportLease lease, CancellationToken ct = default)
        => MutateLeaseAsync(lease, document =>
        {
            document.LeaseToken = null; document.LeaseExpiresOn = null;
            if (document.Attempt >= MaximumAttempts) { document.State = "ManualReview"; document.NextAttemptOn = null; }
            else { document.State = "Pending"; document.NextAttemptOn = DateTimeOffset.UtcNow.AddMinutes(1); }
            return true;
        }, ct);

    public async Task<IReadOnlyList<ContentImportJob>> ListRunnableAsync(DateTimeOffset now, int take, CancellationToken ct = default)
    {
        if (take is <= 0 or > MaximumRunnableTake) return [];
        await using var session = await store.OpenSessionAsync(new SessionOptions(), ct);
        // Recover one capped stale job per poll before selecting normal work. It is intentionally bounded.
        var exhausted = await session.Query<ContentImportJobDocument>()
            .Where(x => x.Attempt >= MaximumAttempts && (x.State == "Pending" || (x.State == "Running" && x.LeaseExpiresOn <= now)))
            .OrderBy(x => x.CreatedOn).Take(1).FirstOrDefaultAsync(ct);
        if (exhausted is not null)
        {
            exhausted.State = "ManualReview"; exhausted.LeaseToken = null; exhausted.LeaseExpiresOn = null; exhausted.NextAttemptOn = null; exhausted.ModifiedOn = now;
            session.Store(exhausted);
            try { await session.SaveChangesAsync(ct); }
            catch (ConcurrencyException) { session.ClearChanges(); }
        }
        return (await session.Query<ContentImportJobDocument>()
            .Where(x => (x.State == "Pending" && x.Attempt < MaximumAttempts && (x.NextAttemptOn == null || x.NextAttemptOn <= now)) || (x.State == "Running" && x.Attempt < MaximumAttempts && x.LeaseExpiresOn <= now))
            .OrderBy(x => x.CreatedOn).Take(take).ToListAsync(ct)).Select(Map).ToArray();
    }

    private async Task<bool> MutateLeaseAsync(ContentImportLease lease, Func<ContentImportJobDocument, bool> mutation, CancellationToken ct)
    {
        if (lease.JobId <= 0 || !IsBounded(lease.Token, 128) || lease.FencingVersion <= 0) return false;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var session = await store.OpenSessionAsync(new SessionOptions(), ct);
            var document = await session.LoadAsync<ContentImportJobDocument>(lease.JobId, ct);
            if (document is null || document.State != "Running" || document.LeaseExpiresOn is not { } expiry || expiry <= DateTimeOffset.UtcNow
                || !string.Equals(document.LeaseToken, lease.Token, StringComparison.Ordinal) || document.FencingVersion != lease.FencingVersion) return false;
            if (!mutation(document)) return false;
            document.ModifiedOn = DateTimeOffset.UtcNow; session.Store(document);
            try { await session.SaveChangesAsync(ct); return true; }
            catch (ConcurrencyException) { session.ClearChanges(); }
        }
        return false;
    }

    private static ContentImportJobDocument New(ContentImportRequest request, long tenantId) => new()
    {
        Id = SnowflakeGenerator.NewId(), RequestIdentity = request.Identity, TenantId = tenantId, SiteId = request.SiteId,
        ImporterKey = request.ImporterKey, ImporterVersion = request.ImporterVersion, SourceFingerprint = request.SourceFingerprint,
        SelectionFingerprint = request.SelectionFingerprint, OptionsJson = request.CanonicalOptionsJson, Actor = request.Actor, Activate = request.Activate
    };
    private static ContentImportJob Map(ContentImportJobDocument source) => new(source.Id, source.RequestIdentity, source.TenantId,
        new(source.SiteId, source.ImporterKey, source.ImporterVersion, source.SourceFingerprint, source.SelectionFingerprint, source.OptionsJson, source.Actor, source.Activate),
        Enum.Parse<ContentImportJobState>(source.State, true), source.Attempt, source.Checkpoint, source.ProgressCurrent, source.ProgressTotal, source.LastError,
        source.LeaseToken, source.FencingVersion, source.LeaseExpiresOn, source.NextAttemptOn, source.CreatedOn, source.ModifiedOn);
    private static bool IsUniqueConflict(Exception ex) => ex.ToString().Contains("unique", StringComparison.OrdinalIgnoreCase) || ex.ToString().Contains("already exists", StringComparison.OrdinalIgnoreCase);
    private static TimeSpan Backoff(int attempt) => TimeSpan.FromMinutes(Math.Min(60, 1 << Math.Min(6, Math.Max(0, attempt - 1))));
    private static bool IsBoundedError(string? error) => !string.IsNullOrWhiteSpace(error) && error.Length <= MaximumErrorLength;
    private static bool IsValidFailure(string? checkpoint, long? current, long? total, string? error)
        => (checkpoint is null || checkpoint.Length <= MaximumCheckpointLength)
            && (current is null ? total is null : IsValidProgress(checkpoint, current.Value, total))
            && IsBoundedError(error);
    private static bool IsBounded(string? value, int maximumLength) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;
    private static bool IsValidProgress(string? checkpoint, long current, long? total) => current >= 0 && (total is null || total >= current) && (checkpoint is null || checkpoint.Length <= MaximumCheckpointLength);
    private static bool ApplyProgress(ContentImportJobDocument document, string? checkpoint, long current, long? total)
    {
        if (current < document.ProgressCurrent || (document.ProgressTotal is { } previousTotal && (total is null || total < previousTotal)) || (total is { } suppliedTotal && suppliedTotal < current)) return false;
        document.Checkpoint = checkpoint; document.ProgressCurrent = current; document.ProgressTotal = total;
        return true;
    }
    private static bool ApplyOptionalProgress(ContentImportJobDocument document, string? checkpoint, long? current, long? total)
    {
        if (current is null)
        {
            if (checkpoint is not null) document.Checkpoint = checkpoint;
            return true;
        }
        return ApplyProgress(document, checkpoint ?? document.Checkpoint, current.Value, total);
    }
}
