using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Cms.Data.Queries.Base;
using Aero.Core.Entities;
using JasperFx.Core;
using Marten;
using Marten.Linq;
using static System.Collections.Specialized.BitVector32;

namespace Aero.Cms.Data.Repositories;

public interface IAliasRepository : IMartenCompiledRepository<AliasDocument>
{
    Task<IList<AliasDocument>> GetBySiteIdAsync(long siteId, CancellationToken cancellationToken = default);
    Task<AliasDocument?> GetByOldPathAsync(string oldPath, CancellationToken cancellationToken = default);
    Task<AliasDocument?> GetByOldPathAsync(long siteId, string oldPath, CancellationToken cancellationToken = default);
    Task<IList<AliasDocument>> GetByNewPathAsync(string newPath, CancellationToken cancellationToken = default);
    Task<IList<AliasDocument>> GetByNewPathAsync(long siteId, string newPath, CancellationToken cancellationToken = default);
    Task<IList<AliasDocument>> GetByNotesAsync(string notes, CancellationToken cancellationToken = default);
    Task<IList<AliasDocument>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    Task<IList<AliasDocument>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

public sealed class AliasRepository : MartenCompiledRepository<AliasDocument>, IAliasRepository
{
    public AliasRepository(IDocumentSession session) : base(session)
    {
    }

    protected override EntityByIdQuery<AliasDocument> CreateByIdQuery(long id)
        => new AliasByIdQuery { Id = id };

    protected override EntitiesByIdsQuery<AliasDocument> CreateByIdsQuery(IEnumerable<long> ids)
    {
        var query = new AliasesByIdsQuery()
        {
            Ids = ids
        };
        return query;
    }

    public async Task<IList<AliasDocument>> GetBySiteIdAsync(long siteId, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesBySiteIdQuery { SiteId = siteId }, cancellationToken);

    public Task<AliasDocument?> GetByOldPathAsync(string oldPath, CancellationToken cancellationToken = default)
        => Session.QueryAsync(new AliasByOldPathQuery { OldPath = oldPath }, cancellationToken);

    public Task<AliasDocument?> GetByOldPathAsync(long siteId, string oldPath, CancellationToken cancellationToken = default)
        => Session.QueryAsync(new AliasByOldPathAndSiteIdQuery { SiteId = siteId, OldPath = oldPath }, cancellationToken);

    public async Task<IList<AliasDocument>> GetByNewPathAsync(string newPath, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesByNewPathQuery { NewPath = newPath }, cancellationToken);

    public async Task<IList<AliasDocument>> GetByNewPathAsync(long siteId, string newPath, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesBySiteIdAndNewPathQuery { SiteId = siteId, NewPath = newPath }, cancellationToken);

    public async Task<IList<AliasDocument>> GetByNotesAsync(string notes, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesByNotesQuery { Notes = notes }, cancellationToken);

    public async Task<IList<AliasDocument>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesCreatedInRangeQuery { From = from, To = to }, cancellationToken);

    public async Task<IList<AliasDocument>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new AliasesModifiedInRangeQuery { From = from, To = to }, cancellationToken);
}