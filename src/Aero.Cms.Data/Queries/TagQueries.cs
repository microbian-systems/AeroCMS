using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using AeroDB.Sable;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries;


/// <inheritdoc cref="EntityByIdQuery{T}"/>
public sealed class TagByIdQuery : EntityByIdQuery<TagModel>;

/// <inheritdoc cref="EntitiesByIdsQuery{T}"/>
public sealed class TagsByIdsQuery : EntitiesByIdsQuery<TagModel>;

/// <summary>Selects tags whose stored name exactly matches a supplied value.</summary>
/// <remarks>The expression performs no normalization and orders matches by stored name.</remarks>
public sealed class TagsByNameQuery : ICompiledQuery<TagModel, IList<TagModel>>
{
    /// <summary>The name value used by the equality predicate.</summary>
public required string Name { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<TagModel>, IList<TagModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Name == Name)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>Selects tags whose non-null stored name contains a supplied substring.</summary>
/// <remarks>The expression performs no normalization and orders matches by stored name.</remarks>
public sealed class TagsByNameContainsQuery : ICompiledQuery<TagModel, IList<TagModel>>
{
    /// <summary>The substring passed to <see cref="string.Contains(string)"/>.</summary>
public required string Name { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<TagModel>, IList<TagModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Name != null && x.Name.Contains(Name))
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <summary>Selects tags whose stored description exactly matches a supplied value.</summary>
/// <remarks>The expression performs no normalization and orders matches by stored name.</remarks>
public sealed class TagsByDescriptionQuery : ICompiledQuery<TagModel, IList<TagModel>>
{
    /// <summary>The description value used by the equality predicate.</summary>
public required string Description { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<TagModel>, IList<TagModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Description == Description)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <inheritdoc cref="EntitiesCreatedInRangeQuery{T}"/>
public sealed class TagsCreatedInRangeQuery : EntitiesCreatedInRangeQuery<TagModel>;

/// <inheritdoc cref="EntitiesModifiedInRangeQuery{T}"/>
public sealed class TagsModifiedInRangeQuery : EntitiesModifiedInRangeQuery<TagModel>;
