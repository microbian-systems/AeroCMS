using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Services;

/// <summary>
/// Describes one previously published page route that should resolve to its new canonical route.
/// Paths are culture-neutral and begin with a slash.
/// </summary>
/// <param name="PageId">The page identifier recorded as the owner of an automatic alias.</param>
/// <param name="SiteId">The site identifier used to scope alias lookups and uniqueness checks.</param>
/// <param name="Culture">The culture label to normalize and use as part of the alias scope.</param>
/// <param name="OldPath">The prior public URL path that may be preserved as an alias source.</param>
/// <param name="NewPath">The canonical public URL path to which owned aliases should point.</param>
/// <param name="PreserveOldPath">Whether the writer should create the prior route when no owned alias exists.</param>
/// <remarks>
/// This transport record performs no validation or normalization itself. Implementations define how malformed,
/// duplicate, cross-site, or conflicting candidates are handled.
/// </remarks>
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
/// <param name="Created">The number of alias documents newly staged for storage.</param>
/// <param name="Updated">The number of existing alias documents staged with a changed redirect target.</param>
/// <param name="Deleted">The number of alias documents staged for deletion.</param>
/// <remarks>The counts describe staged operations, not a confirmed persistence commit or cache refresh.</remarks>
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
/// <remarks>
/// Session ownership, commit/rollback, retry, and concurrency handling remain with the caller. The contract scopes
/// candidates by explicit site and culture values and does not infer a tenant, authenticated user, or request culture.
/// </remarks>
public interface IPageRouteAliasWriter
{
    /// <summary>
    /// Stages the requested automatic aliases without committing the session.
    /// </summary>
    /// <param name="session">The caller-owned document session in which alias stores and deletes are staged.</param>
    /// <param name="candidates">The route-change candidates to process.</param>
    /// <param name="cancellationToken">Token forwarded to document queries.</param>
    /// <returns>
    /// A successful result containing staged mutation counts, or a failure such as an alias-ownership conflict.
    /// </returns>
    /// <remarks>
    /// A failure does not commit or roll back the supplied session and may occur after earlier candidates staged
    /// changes. Cancellation and operational exceptions are not required to be converted into
    /// <see cref="Result{T,TError}"/> and may propagate.
    /// </remarks>
    Task<Result<PageRouteAliasStageResult, AeroError>> StageAsync(
        IDocumentSession session,
        IReadOnlyList<PageRouteAliasCandidate> candidates,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates and refreshes the alias read cache after the caller commits
    /// staged alias mutations.
    /// </summary>
    /// <param name="cancellationToken">Token forwarded to asynchronous cache refresh.</param>
    /// <returns>A task representing cache invalidation and refresh.</returns>
    /// <remarks>
    /// This operation is separate from persistence and supplies no transaction, distributed-coherence, or rollback
    /// guarantee. Callers must not invoke it as proof that staged changes committed.
    /// </remarks>
    Task OnCommittedAsync(CancellationToken cancellationToken = default);
}
