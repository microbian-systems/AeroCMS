using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Cms.Data.Queries.Base;
using Marten;

namespace Aero.Cms.Data.Repositories;

/// <summary>
/// Defines an interface for IAliasRepository.
/// </summary>
public interface IAliasRepository : IMartenCompiledRepository<AliasDocument>
{
        /// <summary>
    /// GetBySiteIdAsync method.
    /// </summary>
Task<IList<AliasDocument>> GetBySiteIdAsync(long siteId, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetByOldPathAsync method.
    /// </summary>
Task<AliasDocument?> GetByOldPathAsync(string oldPath, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetByOldPathAsync method.
    /// </summary>
Task<AliasDocument?> GetByOldPathAsync(long siteId, string oldPath, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetByNewPathAsync method.
    /// </summary>
Task<IList<AliasDocument>> GetByNewPathAsync(string newPath, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetByNewPathAsync method.
    /// </summary>
Task<IList<AliasDocument>> GetByNewPathAsync(long siteId, string newPath, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetByNotesAsync method.
    /// </summary>
Task<IList<AliasDocument>> GetByNotesAsync(string notes, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetCreatedInRangeAsync method.
    /// </summary>
Task<IList<AliasDocument>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetModifiedInRangeAsync method.
    /// </summary>
Task<IList<AliasDocument>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a class for AliasRepository.
/// </summary>
public sealed class AliasRepository : MartenCompiledRepository<AliasDocument>, IAliasRepository
{
        /// <summary>
    /// Initializes a new instance of the <see cref="AliasRepository"/> class.
    /// </summary>
public AliasRepository(IDocumentSession session) : base(session)
    {
    }

        /// <summary>
    /// CreateByIdQuery method.
    /// </summary>
protected override EntityByIdQuery<AliasDocument> CreateByIdQuery(long id)
        => new AliasByIdQuery { Id = id };

        /// <summary>
    /// CreateByIdsQuery method.
    /// </summary>
protected override EntitiesByIdsQuery<AliasDocument> CreateByIdsQuery(IEnumerable<long> ids)
    {
        var query = new AliasesByIdsQuery()
        {
            Ids = ids
        };
        return query;
    }

        /// <summary>
    /// GetBySiteIdAsync method.
    /// </summary>
public async Task<IList<AliasDocument>> GetBySiteIdAsync(long siteId, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesBySiteIdQuery { SiteId = siteId }, cancellationToken);

        /// <summary>
    /// GetByOldPathAsync method.
    /// </summary>
public Task<AliasDocument?> GetByOldPathAsync(string oldPath, CancellationToken cancellationToken = default)
        => Session.QueryAsync(new AliasByOldPathQuery { OldPath = oldPath }, cancellationToken);

        /// <summary>
    /// GetByOldPathAsync method.
    /// </summary>
public Task<AliasDocument?> GetByOldPathAsync(long siteId, string oldPath, CancellationToken cancellationToken = default)
        => Session.QueryAsync(new AliasByOldPathAndSiteIdQuery { SiteId = siteId, OldPath = oldPath }, cancellationToken);

        /// <summary>
    /// GetByNewPathAsync method.
    /// </summary>
public async Task<IList<AliasDocument>> GetByNewPathAsync(string newPath, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesByNewPathQuery { NewPath = newPath }, cancellationToken);

        /// <summary>
    /// GetByNewPathAsync method.
    /// </summary>
public async Task<IList<AliasDocument>> GetByNewPathAsync(long siteId, string newPath, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesBySiteIdAndNewPathQuery { SiteId = siteId, NewPath = newPath }, cancellationToken);

        /// <summary>
    /// GetByNotesAsync method.
    /// </summary>
public async Task<IList<AliasDocument>> GetByNotesAsync(string notes, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesByNotesQuery { Notes = notes }, cancellationToken);

        /// <summary>
    /// GetCreatedInRangeAsync method.
    /// </summary>
public async Task<IList<AliasDocument>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesCreatedInRangeQuery { From = from, To = to }, cancellationToken);

        /// <summary>
    /// GetModifiedInRangeAsync method.
    /// </summary>
public async Task<IList<AliasDocument>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesModifiedInRangeQuery { From = from, To = to }, cancellationToken);
}