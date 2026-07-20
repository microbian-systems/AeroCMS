using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using AeroDB.Sable;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries;


/// <inheritdoc cref="EntityByIdQuery{T}"/>
public sealed class TenantByIdQuery : EntityByIdQuery<TenantModel>;

/// <inheritdoc cref="EntitiesByIdsQuery{T}"/>
public sealed class TenantsByIdsQuery : EntitiesByIdsQuery<TenantModel>;

/// <summary>Selects the first tenant whose stored name exactly matches a supplied value.</summary>
/// <remarks>The expression performs no normalization and returns <see langword="null"/> when no tenant matches.</remarks>
public sealed class TenantByNameQuery : ICompiledQuery<TenantModel, TenantModel?>
{
    /// <summary>The tenant name used by the equality predicate.</summary>
public required string Name { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<TenantModel>, TenantModel?>> QueryIs()
    {
        return q => q
            .FirstOrDefault(x => x.Name == Name);
    }
}

/// <summary>Selects the first tenant whose stored hostname exactly matches a supplied value.</summary>
/// <remarks>The expression performs no hostname normalization and returns <see langword="null"/> when no tenant matches.</remarks>
public sealed class TenantByHostnameQuery : ICompiledQuery<TenantModel, TenantModel?>
{
    /// <summary>The hostname value used by the equality predicate.</summary>
public required string Hostname { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<TenantModel>, TenantModel?>> QueryIs()
    {
        return q => q
            .FirstOrDefault(x => x.Hostname == Hostname);
    }
}

/// <summary>Selects tenants whose stored notes exactly match a supplied value.</summary>
/// <remarks>The expression performs no normalization and orders matches by stored name.</remarks>
public sealed class TenantsByNotesQuery : ICompiledQuery<TenantModel, IList<TenantModel>>
{
    /// <summary>The notes value used by the equality predicate.</summary>
public required string Notes { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<TenantModel>, IList<TenantModel>>> QueryIs()
    {
        return q => q
            .Where(x => x.Notes == Notes)
            .OrderBy(x => x.Name)
            .ToList();
    }
}

/// <inheritdoc cref="EntitiesCreatedInRangeQuery{T}"/>
public sealed class TenantsCreatedInRangeQuery : EntitiesCreatedInRangeQuery<TenantModel>;

/// <inheritdoc cref="EntitiesModifiedInRangeQuery{T}"/>
public sealed class TenantsModifiedInRangeQuery : EntitiesModifiedInRangeQuery<TenantModel>;
