using Aero.Cms.Core.Entities;
using Aero.Cms.Services;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Stages automatic aliases for published page-route changes in the caller's
/// Sable unit of work. This type never commits the supplied session: callers
/// must call <see cref="OnCommittedAsync"/> only after their surrounding
/// transaction has committed successfully.
/// </summary>
public sealed class PageRouteAliasWriter(IAliasRuleCache? cache = null) : IPageRouteAliasWriter
{
    /// <inheritdoc />
    public async Task<Result<PageRouteAliasStageResult, AeroError>> StageAsync(
        IDocumentSession session,
        IReadOnlyList<PageRouteAliasCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var created = 0;
        var updated = 0;
        var deleted = 0;

        foreach (var candidate in candidates
                     .DistinctBy(x => new
                     {
                         x.SiteId,
                         x.Culture,
                         x.PageId,
                         x.OldPath,
                         x.NewPath,
                         x.PreserveOldPath
                     }))
        {
            var culture = AliasDocument.NormalizeCulture(candidate.Culture);
            var oldPath = AliasDocument.NormalizePath(candidate.OldPath);
            var newPath = AliasDocument.NormalizePath(candidate.NewPath);
            if (string.Equals(oldPath, newPath, StringComparison.Ordinal))
                continue;

            var reclaimed = await session.Query<AliasDocument>()
                .FirstOrDefaultAsync(
                    x => x.SiteId == candidate.SiteId
                         && x.Culture == culture
                         && x.NormalizedOldPath == newPath,
                    cancellationToken);
            long? reclaimedAliasId = null;
            if (reclaimed is not null)
            {
                if (reclaimed.OwnerId != candidate.PageId
                    || !string.Equals(reclaimed.OwnerType, "Page", StringComparison.Ordinal))
                {
                    return new Result<PageRouteAliasStageResult, AeroError>.Failure(
                        AeroError.ConflictError(
                            $"The new route '{newPath}' is reserved by another URL alias."));
                }

                reclaimedAliasId = reclaimed.Id;
                session.Delete(reclaimed);
                deleted++;
            }

            var ownerAliases = await session.Query<AliasDocument>()
                .Where(x => x.SiteId == candidate.SiteId
                            && x.Culture == culture
                            && x.OwnerId == candidate.PageId
                            && x.OwnerType == "Page")
                .ToListAsync(cancellationToken);
            if (reclaimedAliasId.HasValue)
                ownerAliases.RemoveAll(x => x.Id == reclaimedAliasId.Value);

            var oldAlias = ownerAliases.FirstOrDefault(x =>
                string.Equals(x.NormalizedOldPath, oldPath, StringComparison.Ordinal));
            if (oldAlias is null && candidate.PreserveOldPath)
            {
                var occupied = await session.Query<AliasDocument>()
                    .FirstOrDefaultAsync(
                        x => x.SiteId == candidate.SiteId
                             && x.Culture == culture
                             && x.NormalizedOldPath == oldPath,
                        cancellationToken);
                if (occupied is not null)
                {
                    return new Result<PageRouteAliasStageResult, AeroError>.Failure(
                        AeroError.ConflictError(
                            $"The previous route '{oldPath}' is already owned by another URL alias."));
                }

                oldAlias = new AliasDocument
                {
                    Id = Snowflake.NewId(),
                    SiteId = candidate.SiteId,
                    Culture = culture,
                    OwnerId = candidate.PageId,
                    OwnerType = "Page",
                    IsAutomatic = true,
                    OldPath = oldPath,
                    NormalizedOldPath = oldPath,
                    NewPath = newPath,
                    StatusCode = StatusCodes.Status301MovedPermanently,
                    Notes = "Automatically preserved after a published page route change.",
                    CreatedBy = "system"
                };
                session.Store(oldAlias);
                ownerAliases.Add(oldAlias);
                created++;
            }

            foreach (var alias in ownerAliases.Where(x =>
                         !string.Equals(x.NewPath, newPath, StringComparison.Ordinal)))
            {
                alias.NewPath = newPath;
                alias.ModifiedOn = DateTimeOffset.UtcNow;
                alias.ModifiedBy = "system";
                session.Store(alias);
                updated++;
            }
        }

        return new Result<PageRouteAliasStageResult, AeroError>.Ok(
            new PageRouteAliasStageResult(created, updated, deleted));
    }

    /// <summary>
    /// Invalidates and refreshes the optional cache after a successful caller
    /// commit. If no cache was supplied, this is a no-op. Cancellation is passed
    /// to the refresh; it does not roll back the already committed route change.
    /// </summary>
    public async Task OnCommittedAsync(CancellationToken cancellationToken = default)
    {
        if (cache is null)
            return;

        // Invalidate first so a failed refresh can never serve a stale redirect.
        cache.Invalidate();
        await cache.RefreshAsync(cancellationToken);
    }
}
