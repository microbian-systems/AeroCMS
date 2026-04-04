using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Cms.Data.Queries.Base;
using JasperFx.Core;
using Marten;
using Marten.Linq;

namespace Aero.Cms.Data.Repositories;

public interface ITagRepository : IMartenCompiledRepository<TagModel>
{
    Task<IList<TagModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IList<TagModel>> GetByDescriptionAsync(string description, CancellationToken cancellationToken = default);
    Task<IList<TagModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    Task<IList<TagModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

public sealed class TagRepository : MartenCompiledRepository<TagModel>, ITagRepository
{
    public TagRepository(IDocumentSession session) : base(session)
    {
    }

    protected override EntityByIdQuery<TagModel> CreateByIdQuery(long id)
        => new TagByIdQuery { Id = id };

    protected override EntitiesByIdsQuery<TagModel> CreateByIdsQuery(IEnumerable<long> ids)
    {
        var query = new TagsByIdsQuery()
        {
            Ids = ids
        };
        return query;
    }

    public async Task<IList<TagModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TagsByNameQuery { Name = name }, cancellationToken);

    public async Task<IList<TagModel>> GetByDescriptionAsync(string description, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TagsByDescriptionQuery { Description = description }, cancellationToken);

    public async Task<IList<TagModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TagsCreatedInRangeQuery { From = from, To = to }, cancellationToken);

    public async Task<IList<TagModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TagsModifiedInRangeQuery { From = from, To = to }, cancellationToken);
}
