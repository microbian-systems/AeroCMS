using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Cms.Data.Queries.Base;
using AeroDB.Sable;

namespace Aero.Cms.Data.Repositories;


/// <summary>Defines session-backed persistence and compiled-query operations for categories.</summary>
/// <remarks>
/// String predicates use supplied values exactly and perform no normalization.
/// Cancellation and provider failures from query execution propagate to the caller.
/// </remarks>
public interface ICategoryRepository : IAeroCompiledRepository<CategoryModel>
{
    /// <summary>Returns the first category whose stored slug exactly matches a supplied value.</summary>
    /// <param name="slug">The unmodified slug value to match.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>The first match, or <see langword="null"/> when none exists.</returns>
Task<CategoryModel?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    /// <summary>Returns categories whose stored name exactly matches a supplied value.</summary>
    /// <param name="name">The name value to match without normalization.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Matches ordered by stored name, or an empty list.</returns>
Task<IList<CategoryModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    /// <summary>Returns direct children of the specified category.</summary>
    /// <param name="parentCategoryId">The parent category identifier to match.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Matches ordered by stored name, or an empty list.</returns>
Task<IList<CategoryModel>> GetByParentIdAsync(long parentCategoryId, CancellationToken cancellationToken = default);
    /// <summary>Returns categories whose parent identifier is <see langword="null"/>.</summary>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Root categories ordered by stored name, or an empty list.</returns>
Task<IList<CategoryModel>> GetRootCategoriesAsync(CancellationToken cancellationToken = default);
    /// <summary>Returns categories created in the half-open interval from <paramref name="from"/> through, but excluding, <paramref name="to"/>.</summary>
    /// <param name="from">The inclusive creation-time lower bound.</param>
    /// <param name="to">The exclusive creation-time upper bound.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>The matching categories without a guaranteed order.</returns>
Task<IList<CategoryModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    /// <summary>Returns categories modified in the half-open interval from <paramref name="from"/> through, but excluding, <paramref name="to"/>.</summary>
    /// <param name="from">The inclusive modification-time lower bound.</param>
    /// <param name="to">The exclusive modification-time upper bound.</param>
    /// <param name="cancellationToken">Token forwarded to query execution.</param>
    /// <returns>Matching categories with non-null modification timestamps, without a guaranteed order.</returns>
Task<IList<CategoryModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

/// <summary>Executes category operations through a caller-owned Sable document session.</summary>
public sealed class CategoryRepository : AeroCompiledRepository<CategoryModel>, ICategoryRepository
{
    /// <summary>Initializes a repository that uses the supplied session for all reads and staged writes.</summary>
    /// <param name="session">The caller-owned document session.</param>
public CategoryRepository(IDocumentSession session) : base(session)
    {
    }

    /// <inheritdoc />
    /// <remarks>No current repository operation invokes this override.</remarks>
protected override EntityByIdQuery<CategoryModel> CreateByIdQuery(long id)
        => new CategoryByIdQuery { Id = id };

    /// <inheritdoc />
    /// <remarks>No current repository operation invokes this override.</remarks>
protected override EntitiesByIdsQuery<CategoryModel> CreateByIdsQuery(IEnumerable<long> ids)
    {
        var query = new CategoriesByIdsQuery()
        {
            Ids = ids
        };
        
        return query;
    }

    /// <inheritdoc />
public Task<CategoryModel?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => Session.QueryAsync(new CategoryBySlugQuery { Slug = slug }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<CategoryModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new CategoriesByNameQuery { Name = name }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<CategoryModel>> GetByParentIdAsync(long parentCategoryId, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new CategoriesByParentIdQuery { ParentCategoryId = parentCategoryId }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<CategoryModel>> GetRootCategoriesAsync(CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new RootCategoriesQuery(), cancellationToken);

    /// <inheritdoc />
public async Task<IList<CategoryModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new CategoriesCreatedInRangeQuery { From = from, To = to }, cancellationToken);

    /// <inheritdoc />
public async Task<IList<CategoryModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new CategoriesModifiedInRangeQuery { From = from, To = to }, cancellationToken);
}

// ============================================================
// Tags
// ============================================================



// ============================================================
// Tenants
// ============================================================



// ============================================================
// Sites
// ============================================================



// ============================================================
// Aliases
// ============================================================

