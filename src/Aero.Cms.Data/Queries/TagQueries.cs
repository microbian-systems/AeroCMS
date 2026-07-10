using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using AeroDB.Sable;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries;


/// <summary>
/// Represents a class for TagByIdQuery.
/// </summary>
public sealed class TagByIdQuery : EntityByIdQuery<TagModel>;

/// <summary>
/// Represents a class for TagsByIdsQuery.
/// </summary>
public sealed class TagsByIdsQuery : EntitiesByIdsQuery<TagModel>;

/// <summary>
/// Represents a class for TagsByNameQuery.
/// </summary>
public sealed class TagsByNameQuery : ICompiledQuery<TagModel, IList<TagModel>>
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public required string Name { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<TagModel>, IList<TagModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Name == Name)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>
/// Represents a class for TagsByNameContainsQuery.
/// </summary>
public sealed class TagsByNameContainsQuery : ICompiledQuery<TagModel, IList<TagModel>>
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public required string Name { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<TagModel>, IList<TagModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Name != null && x.Name.Contains(Name))
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>
/// Represents a class for TagsByDescriptionQuery.
/// </summary>
public sealed class TagsByDescriptionQuery : ICompiledQuery<TagModel, IList<TagModel>>
{
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public required string Description { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<TagModel>, IList<TagModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Description == Description)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>
/// Represents a class for TagsCreatedInRangeQuery.
/// </summary>
public sealed class TagsCreatedInRangeQuery : EntitiesCreatedInRangeQuery<TagModel>;

/// <summary>
/// Represents a class for TagsModifiedInRangeQuery.
/// </summary>
public sealed class TagsModifiedInRangeQuery : EntitiesModifiedInRangeQuery<TagModel>;