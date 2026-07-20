using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Cms.Data.Queries.Base;
using AeroDB.Sable;

namespace Aero.Cms.Data.Repositories;

/// <summary>Defines session-backed persistence and compiled-query operations for URL aliases.</summary>
/// <remarks>
/// String predicates use the supplied values exactly; callers must normalize paths
/// before calling when normalized matching is required. Cancellation and provider
/// failures from query execution propagate to the caller.
/// </remarks>
public interface IAliasRepository : IAeroCompiledRepository<AliasDocument>
{
    /// <summary>Returns aliases owned by one site, ordered by stored old path.</summary>
    /// <param name="siteId">The site identifier to match.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>The matching aliases, or an empty list when none match.</returns>
Task<IList<AliasDocument>> GetBySiteIdAsync(long siteId, CancellationToken cancellationToken = default);
    /// <summary>Returns the first alias with an exact stored old-path match across all sites.</summary>
    /// <param name="oldPath">The unmodified old-path value to match.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>The first matching alias, or <see langword="null"/> when none matches.</returns>
Task<AliasDocument?> GetByOldPathAsync(string oldPath, CancellationToken cancellationToken = default);
    /// <summary>Returns the first alias matching both a site and an exact stored old path.</summary>
    /// <param name="siteId">The site identifier to match.</param>
    /// <param name="oldPath">The unmodified old-path value to match.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>The first matching alias, or <see langword="null"/> when none matches.</returns>
Task<AliasDocument?> GetByOldPathAsync(long siteId, string oldPath, CancellationToken cancellationToken = default);
    /// <summary>Returns aliases with an exact stored destination-path match across all sites.</summary>
    /// <param name="newPath">The unmodified destination-path value to match.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Matches ordered by stored old path, or an empty list.</returns>
Task<IList<AliasDocument>> GetByNewPathAsync(string newPath, CancellationToken cancellationToken = default);
    /// <summary>Returns aliases matching both a site and an exact stored destination path.</summary>
    /// <param name="siteId">The site identifier to match.</param>
    /// <param name="newPath">The unmodified destination-path value to match.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Matches ordered by stored old path, or an empty list.</returns>
Task<IList<AliasDocument>> GetByNewPathAsync(long siteId, string newPath, CancellationToken cancellationToken = default);
    /// <summary>Returns aliases whose stored notes exactly match a supplied value.</summary>
    /// <param name="notes">The notes value to match without normalization.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Matches ordered by stored old path, or an empty list.</returns>
Task<IList<AliasDocument>> GetByNotesAsync(string notes, CancellationToken cancellationToken = default);
    /// <summary>Returns aliases created in the half-open interval from <paramref name="from"/> through, but excluding, <paramref name="to"/>.</summary>
    /// <param name="from">The inclusive creation-time lower bound.</param>
    /// <param name="to">The exclusive creation-time upper bound.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>The matching aliases without a guaranteed order.</returns>
Task<IList<AliasDocument>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    /// <summary>Returns aliases modified in the half-open interval from <paramref name="from"/> through, but excluding, <paramref name="to"/>.</summary>
    /// <param name="from">The inclusive modification-time lower bound.</param>
    /// <param name="to">The exclusive modification-time upper bound.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Matching aliases with non-null modification timestamps, without a guaranteed order.</returns>
Task<IList<AliasDocument>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

/// <summary>Executes alias operations through a caller-owned Sable document session.</summary>
public sealed class AliasRepository : AeroCompiledRepository<AliasDocument>, IAliasRepository
{
    /// <summary>Initializes a repository that uses the supplied session for all reads and staged writes.</summary>
    /// <param name="session">The caller-owned document session.</param>
public AliasRepository(IDocumentSession session) : base(session)
    {
    }

    /// <inheritdoc />
    /// <remarks>No current repository operation invokes this override.</remarks>
protected override EntityByIdQuery<AliasDocument> CreateByIdQuery(long id)
        => new AliasByIdQuery { Id = id };

    /// <inheritdoc />
    /// <remarks>No current repository operation invokes this override.</remarks>
protected override EntitiesByIdsQuery<AliasDocument> CreateByIdsQuery(IEnumerable<long> ids)
    {
        var query = new AliasesByIdsQuery()
        {
            Ids = ids
        };
        return query;
    }

    /// <inheritdoc />
public async Task<IList<AliasDocument>> GetBySiteIdAsync(long siteId, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesBySiteIdQuery { SiteId = siteId }, cancellationToken);

    /// <inheritdoc />
public Task<AliasDocument?> GetByOldPathAsync(string oldPath, CancellationToken cancellationToken = default)
        => Session.QueryAsync(new AliasByOldPathQuery { OldPath = oldPath }, cancellationToken);

    /// <inheritdoc />
public Task<AliasDocument?> GetByOldPathAsync(long siteId, string oldPath, CancellationToken cancellationToken = default)
        => Session.QueryAsync(new AliasByOldPathAndSiteIdQuery { SiteId = siteId, OldPath = oldPath }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<AliasDocument>> GetByNewPathAsync(string newPath, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesByNewPathQuery { NewPath = newPath }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<AliasDocument>> GetByNewPathAsync(long siteId, string newPath, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesBySiteIdAndNewPathQuery { SiteId = siteId, NewPath = newPath }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<AliasDocument>> GetByNotesAsync(string notes, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesByNotesQuery { Notes = notes }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<AliasDocument>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesCreatedInRangeQuery { From = from, To = to }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<AliasDocument>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesModifiedInRangeQuery { From = from, To = to }, cancellationToken);
}
