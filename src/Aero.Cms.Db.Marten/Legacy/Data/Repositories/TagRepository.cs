using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Cms.Data.Queries.Base;
using Marten;

namespace Aero.Cms.Data.Repositories;

/// <summary>
/// Defines an interface for ITagRepository.
/// </summary>
public interface ITagRepository : IMartenCompiledRepository<TagModel>
{
        /// <summary>
    /// GetByNameAsync method.
    /// </summary>
Task<IList<TagModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetByDescriptionAsync method.
    /// </summary>
Task<IList<TagModel>> GetByDescriptionAsync(string description, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetCreatedInRangeAsync method.
    /// </summary>
Task<IList<TagModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetModifiedInRangeAsync method.
    /// </summary>
Task<IList<TagModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a class for TagRepository.
/// </summary>
public sealed class TagRepository : MartenCompiledRepository<TagModel>, ITagRepository
{
        /// <summary>
    /// Initializes a new instance of the <see cref="TagRepository"/> class.
    /// </summary>
public TagRepository(IDocumentSession session) : base(session)
    {
    }

        /// <summary>
    /// CreateByIdQuery method.
    /// </summary>
protected override EntityByIdQuery<TagModel> CreateByIdQuery(long id)
        => new TagByIdQuery { Id = id };

        /// <summary>
    /// CreateByIdsQuery method.
    /// </summary>
protected override EntitiesByIdsQuery<TagModel> CreateByIdsQuery(IEnumerable<long> ids)
    {
        var query = new TagsByIdsQuery()
        {
            Ids = ids
        };
        return query;
    }

        /// <summary>
    /// GetByNameAsync method.
    /// </summary>
public async Task<IList<TagModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TagsByNameQuery { Name = name }, cancellationToken);

        /// <summary>
    /// GetByDescriptionAsync method.
    /// </summary>
public async Task<IList<TagModel>> GetByDescriptionAsync(string description, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TagsByDescriptionQuery { Description = description }, cancellationToken);

        /// <summary>
    /// GetCreatedInRangeAsync method.
    /// </summary>
public async Task<IList<TagModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TagsCreatedInRangeQuery { From = from, To = to }, cancellationToken);

        /// <summary>
    /// GetModifiedInRangeAsync method.
    /// </summary>
public async Task<IList<TagModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TagsModifiedInRangeQuery { From = from, To = to }, cancellationToken);
}
