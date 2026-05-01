using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using Marten.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Aero.Cms.Data.Queries;


public sealed class CategoryByIdQuery : EntityByIdQuery<CategoryModel>;

public sealed class CategoriesByIdsQuery : EntitiesByIdsQuery<CategoryModel>;

public sealed class CategoriesByNameQuery : ICompiledQuery<CategoryModel, IList<CategoryModel>>
{
    public required string Name { get; set; }

    public Expression<Func<IMartenQueryable<CategoryModel>, IList<CategoryModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Name == Name)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

public sealed class CategoriesByNameContainsQuery : ICompiledQuery<CategoryModel, IList<CategoryModel>>
{
    public required string Name { get; set; }

    public Expression<Func<IMartenQueryable<CategoryModel>, IList<CategoryModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Name != null && x.Name.Contains(Name))
            .OrderBy(x => x.Name)
            .ToList();
    }
}

public sealed class CategoryBySlugQuery : ICompiledQuery<CategoryModel, CategoryModel?>
{
    public required string Slug { get; set; }

    public Expression<Func<IMartenQueryable<CategoryModel>, CategoryModel?>> QueryIs()
    {
        return q => q
            .FirstOrDefault(x => x.Slug == Slug);
    }
}

public sealed class CategoriesByParentIdQuery : ICompiledQuery<CategoryModel, IList<CategoryModel>>
{
    public required long ParentCategoryId { get; set; }

    public Expression<Func<IMartenQueryable<CategoryModel>, IList<CategoryModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.ParentCategoryId == ParentCategoryId)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

public sealed class RootCategoriesQuery : ICompiledQuery<CategoryModel, IList<CategoryModel>>
{
    public Expression<Func<IMartenQueryable<CategoryModel>, IList<CategoryModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.ParentCategoryId == null)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

public sealed class CategoriesCreatedInRangeQuery : EntitiesCreatedInRangeQuery<CategoryModel>;

public sealed class CategoriesModifiedInRangeQuery : EntitiesModifiedInRangeQuery<CategoryModel>;