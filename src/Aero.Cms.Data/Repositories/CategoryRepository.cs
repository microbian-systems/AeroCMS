using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries;
using Aero.Cms.Data.Queries.Base;
using JasperFx.Core;
using Marten;
using Marten.Linq;

namespace Aero.Cms.Data.Repositories;


public interface ICategoryRepository : IMartenCompiledRepository<CategoryModel>
{
    Task<CategoryModel?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<IList<CategoryModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IList<CategoryModel>> GetByParentIdAsync(long parentCategoryId, CancellationToken cancellationToken = default);
    Task<IList<CategoryModel>> GetRootCategoriesAsync(CancellationToken cancellationToken = default);
    Task<IList<CategoryModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    Task<IList<CategoryModel>> GetModifiedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

public sealed class CategoryRepository : MartenCompiledRepository<CategoryModel>, ICategoryRepository
{
    public CategoryRepository(IDocumentSession session) : base(session)
    {
    }

    protected override EntityByIdQuery<CategoryModel> CreateByIdQuery(long id)
        => new CategoryByIdQuery { Id = id };

    protected override EntitiesByIdsQuery<CategoryModel> CreateByIdsQuery(IEnumerable<long> ids)
    {
        var query = new CategoriesByIdsQuery()
        {
            Ids = ids
        };
        
        return query;
    }

    public Task<CategoryModel?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => Session.QueryAsync(new CategoryBySlugQuery { Slug = slug }, cancellationToken);

    public async Task<IList<CategoryModel>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new CategoriesByNameQuery { Name = name }, cancellationToken);

    public async Task<IList<CategoryModel>> GetByParentIdAsync(long parentCategoryId, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new CategoriesByParentIdQuery { ParentCategoryId = parentCategoryId }, cancellationToken);

    public async Task<IList<CategoryModel>> GetRootCategoriesAsync(CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new RootCategoriesQuery(), cancellationToken);

    public async Task<IList<CategoryModel>> GetCreatedInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await Session.QueryAsync(new CategoriesCreatedInRangeQuery { From = from, To = to }, cancellationToken);

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

