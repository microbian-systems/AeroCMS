using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Services;

/// <summary>
/// Describes one previously published page route that should resolve to its new canonical route.
/// Paths are culture-neutral and begin with a slash.
/// </summary>
public sealed record PageRouteAliasCandidate(
    long PageId,
    long SiteId,
    string Culture,
    string OldPath,
    string NewPath,
    bool PreserveOldPath);

/// <summary>
/// Summarizes alias mutations staged in the caller's document session.
/// </summary>
public sealed record PageRouteAliasStageResult(int Created, int Updated, int Deleted)
{
    /// <summary>
    /// Gets whether the alias cache must be refreshed after the caller commits.
    /// </summary>
    public bool HasChanges => Created > 0 || Updated > 0 || Deleted > 0;
}

/// <summary>
/// Cross-module port used by Pages to stage automatic URL aliases in the same
/// Sable unit of work as the page and slug-reservation mutations.
/// </summary>
public interface IPageRouteAliasWriter
{
    /// <summary>
    /// Stages the requested automatic aliases without committing the session.
    /// </summary>
    Task<Result<PageRouteAliasStageResult, AeroError>> StageAsync(
        IDocumentSession session,
        IReadOnlyList<PageRouteAliasCandidate> candidates,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates and refreshes the alias read cache after the caller commits
    /// staged alias mutations.
    /// </summary>
    Task OnCommittedAsync(CancellationToken cancellationToken = default);
}
