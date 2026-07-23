using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using AeroDB.Sable;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries;


/// <inheritdoc cref="EntityByIdQuery{T}"/>
public sealed class CategoryByIdQuery : EntityByIdQuery<CategoryModel>;

/// <inheritdoc cref="EntitiesByIdsQuery{T}"/>
public sealed class CategoriesByIdsQuery : EntitiesByIdsQuery<CategoryModel>;

/// <summary>Selects categories whose stored name exactly matches a supplied value.</summary>
/// <remarks>The expression performs no normalization and orders matches by stored name.</remarks>
public sealed class CategoriesByNameQuery : ICompiledQuery<CategoryModel, IList<CategoryModel>>
{
    /// <summary>The name value used by the equality predicate.</summary>
public required string Name { get; set; }

    /// <inheritdoc />
public Expression<Func<ISableQueryable<CategoryModel>, IList<CategoryModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Name == Name)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>Selects categories whose non-null stored name contains a supplied substring.</summary>
/// <remarks>The expression performs no normalization and orders matches by stored name.</remarks>
public sealed class CategoriesByNameContainsQuery : ICompiledQuery<CategoryModel, IList<CategoryModel>>
{
    /// <summary>The substring passed to <see cref="string.Contains(string)"/>.</summary>
public required string Name { get; set; }

    /// <inheritdoc />
public Expression<Func<ISableQueryable<CategoryModel>, IList<CategoryModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Name != null && x.Name.Contains(Name))
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>Selects the first category whose stored slug exactly matches a supplied value.</summary>
/// <remarks>The expression performs no slug normalization and returns <see langword="null"/> when no category matches.</remarks>
public sealed class CategoryBySlugQuery : ICompiledQuery<CategoryModel, CategoryModel?>
{
    /// <summary>The slug value used by the equality predicate.</summary>
public required string Slug { get; set; }

    /// <inheritdoc />
public Expression<Func<ISableQueryable<CategoryModel>, CategoryModel?>> QueryIs()
    {
        return q => q
            .FirstOrDefault(x => x.Slug == Slug);
    }
}

/// <summary>Selects direct child categories for a parent identifier.</summary>
/// <remarks>Matches are ordered by stored category name.</remarks>
public sealed class CategoriesByParentIdQuery : ICompiledQuery<CategoryModel, IList<CategoryModel>>
{
    /// <summary>The parent identifier that must match <see cref="CategoryModel.ParentCategoryId"/>.</summary>
public required long ParentCategoryId { get; set; }

    /// <inheritdoc />
public Expression<Func<ISableQueryable<CategoryModel>, IList<CategoryModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.ParentCategoryId == ParentCategoryId)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>Selects categories without a parent, ordered by stored name.</summary>
public sealed class RootCategoriesQuery : ICompiledQuery<CategoryModel, IList<CategoryModel>>
{
    /// <inheritdoc />
public Expression<Func<ISableQueryable<CategoryModel>, IList<CategoryModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.ParentCategoryId == null)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <inheritdoc cref="EntitiesCreatedInRangeQuery{T}"/>
public sealed class CategoriesCreatedInRangeQuery : EntitiesCreatedInRangeQuery<CategoryModel>;

/// <inheritdoc cref="EntitiesModifiedInRangeQuery{T}"/>
public sealed class CategoriesModifiedInRangeQuery : EntitiesModifiedInRangeQuery<CategoryModel>;
