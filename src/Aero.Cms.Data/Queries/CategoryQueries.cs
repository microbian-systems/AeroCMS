using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using AeroDB.Sable;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries;


/// <summary>
/// Represents a class for CategoryByIdQuery.
/// </summary>
public sealed class CategoryByIdQuery : EntityByIdQuery<CategoryModel>;

/// <summary>
/// Represents a class for CategoriesByIdsQuery.
/// </summary>
public sealed class CategoriesByIdsQuery : EntitiesByIdsQuery<CategoryModel>;

/// <summary>
/// Represents a class for CategoriesByNameQuery.
/// </summary>
public sealed class CategoriesByNameQuery : ICompiledQuery<CategoryModel, IList<CategoryModel>>
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public required string Name { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<CategoryModel>, IList<CategoryModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Name == Name)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>
/// Represents a class for CategoriesByNameContainsQuery.
/// </summary>
public sealed class CategoriesByNameContainsQuery : ICompiledQuery<CategoryModel, IList<CategoryModel>>
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public required string Name { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<CategoryModel>, IList<CategoryModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Name != null && x.Name.Contains(Name))
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>
/// Represents a class for CategoryBySlugQuery.
/// </summary>
public sealed class CategoryBySlugQuery : ICompiledQuery<CategoryModel, CategoryModel?>
{
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public required string Slug { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<CategoryModel>, CategoryModel?>> QueryIs()
    {
        return q => q
            .FirstOrDefault(x => x.Slug == Slug);
    }
}

/// <summary>
/// Represents a class for CategoriesByParentIdQuery.
/// </summary>
public sealed class CategoriesByParentIdQuery : ICompiledQuery<CategoryModel, IList<CategoryModel>>
{
        /// <summary>
    /// Gets or sets the Parent Category Id.
    /// </summary>
public required long ParentCategoryId { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<CategoryModel>, IList<CategoryModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.ParentCategoryId == ParentCategoryId)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>
/// Represents a class for RootCategoriesQuery.
/// </summary>
public sealed class RootCategoriesQuery : ICompiledQuery<CategoryModel, IList<CategoryModel>>
{
        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<CategoryModel>, IList<CategoryModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.ParentCategoryId == null)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>
/// Represents a class for CategoriesCreatedInRangeQuery.
/// </summary>
public sealed class CategoriesCreatedInRangeQuery : EntitiesCreatedInRangeQuery<CategoryModel>;

/// <summary>
/// Represents a class for CategoriesModifiedInRangeQuery.
/// </summary>
public sealed class CategoriesModifiedInRangeQuery : EntitiesModifiedInRangeQuery<CategoryModel>;