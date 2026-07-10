using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Cms.Data.Queries.Base;
using AeroDB.Sable;

namespace Aero.Cms.Data.Repositories;


/// <summary>
/// Defines an interface for ICategoryRepository.
/// </summary>
public interface ICategoryRepository : IAeroCompiledRepository<CategoryModel>
{
        /// <summary>
    /// GetBySlugAsync method.
    /// </summary>
Task<CategoryModel?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetByNameAsync method.
    /// </summary>
Task<IList<CategoryModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetByParentIdAsync method.
    /// </summary>
Task<IList<CategoryModel>> GetByParentIdAsync(long parentCategoryId, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetRootCategoriesAsync method.
    /// </summary>
Task<IList<CategoryModel>> GetRootCategoriesAsync(CancellationToken cancellationToken = default);
        /// <summary>
    /// GetCreatedInRangeAsync method.
    /// </summary>
Task<IList<CategoryModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetModifiedInRangeAsync method.
    /// </summary>
Task<IList<CategoryModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a class for CategoryRepository.
/// </summary>
public sealed class CategoryRepository : AeroCompiledRepository<CategoryModel>, ICategoryRepository
{
        /// <summary>
    /// Initializes a new instance of the <see cref="CategoryRepository"/> class.
    /// </summary>
public CategoryRepository(IDocumentSession session) : base(session)
    {
    }

        /// <summary>
    /// CreateByIdQuery method.
    /// </summary>
protected override EntityByIdQuery<CategoryModel> CreateByIdQuery(long id)
        => new CategoryByIdQuery { Id = id };

        /// <summary>
    /// CreateByIdsQuery method.
    /// </summary>
protected override EntitiesByIdsQuery<CategoryModel> CreateByIdsQuery(IEnumerable<long> ids)
    {
        var query = new CategoriesByIdsQuery()
        {
            Ids = ids
        };
        
        return query;
    }

        /// <summary>
    /// GetBySlugAsync method.
    /// </summary>
public Task<CategoryModel?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => Session.QueryAsync(new CategoryBySlugQuery { Slug = slug }, cancellationToken);

        /// <summary>
    /// GetByNameAsync method.
    /// </summary>
public async Task<IList<CategoryModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new CategoriesByNameQuery { Name = name }, cancellationToken);

        /// <summary>
    /// GetByParentIdAsync method.
    /// </summary>
public async Task<IList<CategoryModel>> GetByParentIdAsync(long parentCategoryId, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new CategoriesByParentIdQuery { ParentCategoryId = parentCategoryId }, cancellationToken);

        /// <summary>
    /// GetRootCategoriesAsync method.
    /// </summary>
public async Task<IList<CategoryModel>> GetRootCategoriesAsync(CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new RootCategoriesQuery(), cancellationToken);

        /// <summary>
    /// GetCreatedInRangeAsync method.
    /// </summary>
public async Task<IList<CategoryModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new CategoriesCreatedInRangeQuery { From = from, To = to }, cancellationToken);

        /// <summary>
    /// GetModifiedInRangeAsync method.
    /// </summary>
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

