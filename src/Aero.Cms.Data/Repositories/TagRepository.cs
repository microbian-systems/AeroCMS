using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Cms.Data.Queries.Base;
using AeroDB.Sable;

namespace Aero.Cms.Data.Repositories;

/// <summary>Defines session-backed persistence and compiled-query operations for tags.</summary>
/// <remarks>
/// String predicates use supplied values exactly and perform no normalization.
/// Cancellation and provider failures from query execution propagate to the caller.
/// </remarks>
public interface ITagRepository : IAeroCompiledRepository<TagModel>
{
    /// <summary>Returns tags whose stored name exactly matches a supplied value.</summary>
    /// <param name="name">The name value to match without normalization.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Matches ordered by stored name, or an empty list.</returns>
Task<IList<TagModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    /// <summary>Returns tags whose stored description exactly matches a supplied value.</summary>
    /// <param name="description">The description value to match without normalization.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Matches ordered by stored name, or an empty list.</returns>
Task<IList<TagModel>> GetByDescriptionAsync(string description, CancellationToken cancellationToken = default);
    /// <summary>Returns tags created in the half-open interval from <paramref name="from"/> through, but excluding, <paramref name="to"/>.</summary>
    /// <param name="from">The inclusive creation-time lower bound.</param>
    /// <param name="to">The exclusive creation-time upper bound.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>The matching tags without a guaranteed order.</returns>
Task<IList<TagModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    /// <summary>Returns tags modified in the half-open interval from <paramref name="from"/> through, but excluding, <paramref name="to"/>.</summary>
    /// <param name="from">The inclusive modification-time lower bound.</param>
    /// <param name="to">The exclusive modification-time upper bound.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Matching tags with non-null modification timestamps, without a guaranteed order.</returns>
Task<IList<TagModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

/// <summary>Executes tag operations through a caller-owned Sable document session.</summary>
public sealed class TagRepository : AeroCompiledRepository<TagModel>, ITagRepository
{
    /// <summary>Initializes a repository that uses the supplied session for all reads and staged writes.</summary>
    /// <param name="session">The caller-owned document session.</param>
public TagRepository(IDocumentSession session) : base(session)
    {
    }

    /// <inheritdoc />
    /// <remarks>No current repository operation invokes this override.</remarks>
protected override EntityByIdQuery<TagModel> CreateByIdQuery(long id)
        => new TagByIdQuery { Id = id };

    /// <inheritdoc />
    /// <remarks>No current repository operation invokes this override.</remarks>
protected override EntitiesByIdsQuery<TagModel> CreateByIdsQuery(IEnumerable<long> ids)
    {
        var query = new TagsByIdsQuery()
        {
            Ids = ids
        };
        return query;
    }

    /// <inheritdoc />
public async Task<IList<TagModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TagsByNameQuery { Name = name }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<TagModel>> GetByDescriptionAsync(string description, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TagsByDescriptionQuery { Description = description }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<TagModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TagsCreatedInRangeQuery { From = from, To = to }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<TagModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new TagsModifiedInRangeQuery { From = from, To = to }, cancellationToken);
}
