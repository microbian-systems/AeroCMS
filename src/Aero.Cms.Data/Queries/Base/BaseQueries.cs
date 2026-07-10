using Aero.Core.Entities;
using AeroDB.Sable;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries.Base;

/// <summary>
/// Compiled query to load a single document by its <c>long</c> identity.
/// Uses <c>==</c> directly (not <c>IEquatable&lt;&gt;.Equals()</c>) so AeroDB's LINQ
/// provider can translate it to SQL.
/// </summary>
public class EntityByIdQuery<T> : ICompiledQuery<T, T?>
    where T : class, global::Aero.Core.Entities.IEntity<long>
{
        /// <summary>
    /// Gets or sets the Id.
    /// </summary>
public required long Id { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<T>, T?>> QueryIs()
    {
        return q => q.FirstOrDefault(x => x.Id == Id);
    }
}

/// <summary>
/// Compiled query to load documents by a <c>long</c> identity (returns a list).
/// Uses <c>==</c> for AeroDB-compatible SQL translation.
/// </summary>
public abstract class EntityByIdQueryList<T> : ICompiledQuery<T, IList<T>>
    where T : class, global::Aero.Core.Entities.IEntity<long>
{
        /// <summary>
    /// Gets or sets the Id.
    /// </summary>
public long Id { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => x.Id == Id).ToList();
    }
}

/// <summary>
/// Represents a class for EntitiesByIdsQuery.
/// </summary>
public class EntitiesByIdsQuery<T> : EntitiesByIdsQuery<T, long>
    where T : global::Aero.Core.Entities.Entity;

/// <summary>
/// Represents a class for EntitiesByIdsQuery.
/// </summary>
public class EntitiesByIdsQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : class, global::Aero.Core.Entities.IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
        /// <summary>
    /// Gets or sets the Ids.
    /// </summary>
public IEnumerable<TKey> Ids { get; init; } = [];
        /// <summary>
    /// QueryIs method.
    /// </summary>
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => Ids.Contains(x.Id)).ToList();
    }
}

/// <summary>
/// Represents a class for EntitiesByCreatedByQuery.
/// </summary>
public abstract class EntitiesByCreatedByQuery<T> : EntitiesByCreatedByQuery<T, long>
    where T : global::Aero.Core.Entities.Entity;

/// <summary>
/// Represents a class for EntitiesByCreatedByQuery.
/// </summary>
public abstract class EntitiesByCreatedByQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
        /// <summary>
    /// Gets or sets the Created By.
    /// </summary>
public required string CreatedBy { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => x.CreatedBy == CreatedBy).ToList();
    }
}

/// <summary>
/// Represents a class for EntitiesByModifiedByQuery.
/// </summary>
public abstract class EntitiesByModifiedByQuery<T> : EntitiesByModifiedByQuery<T, long>
    where T : global::Aero.Core.Entities.Entity;

/// <summary>
/// Represents a class for EntitiesByModifiedByQuery.
/// </summary>
public abstract class EntitiesByModifiedByQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
        /// <summary>
    /// Gets or sets the Modified By.
    /// </summary>
public required string ModifiedBy { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => x.ModifiedBy == ModifiedBy).ToList();
    }
}

/// <summary>
/// Represents a class for EntitiesCreatedInRangeQuery.
/// </summary>
public abstract class EntitiesCreatedInRangeQuery<T> : EntitiesCreatedInRangeQuery<T, long>
    where T : global::Aero.Core.Entities.Entity;

/// <summary>
/// Represents a class for EntitiesCreatedInRangeQuery.
/// </summary>
public abstract class EntitiesCreatedInRangeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
        /// <summary>
    /// Gets or sets the From.
    /// </summary>
public required DateTimeOffset From { get; set; }
        /// <summary>
    /// Gets or sets the To.
    /// </summary>
public required DateTimeOffset To { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.CreatedOn >= From && x.CreatedOn < To)
            .ToList();
    }
}

/// <summary>
/// Represents a class for EntitiesModifiedInRangeQuery.
/// </summary>
public abstract class EntitiesModifiedInRangeQuery<T> : EntitiesModifiedInRangeQuery<T, long>
    where T : global::Aero.Core.Entities.Entity;

/// <summary>
/// Represents a class for EntitiesModifiedInRangeQuery.
/// </summary>
public abstract class EntitiesModifiedInRangeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
        /// <summary>
    /// Gets or sets the From.
    /// </summary>
public required DateTimeOffset From { get; set; }
        /// <summary>
    /// Gets or sets the To.
    /// </summary>
public required DateTimeOffset To { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.ModifiedOn != null &&
                        x.ModifiedOn >= From &&
                        x.ModifiedOn < To)
            .ToList();
    }
}


/// <summary>
/// Represents a class for EntitiesCreatedSinceQuery.
/// </summary>
public abstract class EntitiesCreatedSinceQuery<T> : EntitiesCreatedSinceQuery<T, long>
    where T : global::Aero.Core.Entities.Entity;

/// <summary>
/// Represents a class for EntitiesCreatedSinceQuery.
/// </summary>
public abstract class EntitiesCreatedSinceQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
        /// <summary>
    /// Gets or sets the Since.
    /// </summary>
public required DateTimeOffset Since { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => x.CreatedOn >= Since).ToList();
    }
}

/// <summary>
/// Represents a class for EntitiesCreatedBeforeQuery.
/// </summary>
public abstract class EntitiesCreatedBeforeQuery<T> : EntitiesCreatedBeforeQuery<T, long>
    where T : global::Aero.Core.Entities.Entity;

/// <summary>
/// Represents a class for EntitiesCreatedBeforeQuery.
/// </summary>
public abstract class EntitiesCreatedBeforeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
        /// <summary>
    /// Gets or sets the Before.
    /// </summary>
public required DateTimeOffset Before { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => x.CreatedOn < Before).ToList();
    }
}

/// <summary>
/// Represents a class for EntitiesModifiedSinceQuery.
/// </summary>
public abstract class EntitiesModifiedSinceQuery<T> : EntitiesModifiedSinceQuery<T, long>
    where T : global::Aero.Core.Entities.Entity;

/// <summary>
/// Represents a class for EntitiesModifiedSinceQuery.
/// </summary>
public abstract class EntitiesModifiedSinceQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
        /// <summary>
    /// Gets or sets the Since.
    /// </summary>
public required DateTimeOffset Since { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.ModifiedOn != null && x.ModifiedOn >= Since)
            .ToList();
    }
}

/// <summary>
/// Represents a class for EntitiesModifiedBeforeQuery.
/// </summary>
public abstract class EntitiesModifiedBeforeQuery<T> : EntitiesModifiedBeforeQuery<T, long>
    where T : global::Aero.Core.Entities.Entity;

/// <summary>
/// Represents a class for EntitiesModifiedBeforeQuery.
/// </summary>
public abstract class EntitiesModifiedBeforeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
        /// <summary>
    /// Gets or sets the Before.
    /// </summary>
public required DateTimeOffset Before { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.ModifiedOn != null && x.ModifiedOn < Before)
            .ToList();
    }
}

/// <summary>
/// Represents a class for EntitiesByCreatedByInDateRangeQuery.
/// </summary>
public abstract class EntitiesByCreatedByInDateRangeQuery<T> : EntitiesByCreatedByInDateRangeQuery<T, long>
    where T : global::Aero.Core.Entities.Entity;

/// <summary>
/// Represents a class for EntitiesByCreatedByInDateRangeQuery.
/// </summary>
public abstract class EntitiesByCreatedByInDateRangeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
        /// <summary>
    /// Gets or sets the Created By.
    /// </summary>
public required string CreatedBy { get; set; }
        /// <summary>
    /// Gets or sets the From.
    /// </summary>
public required DateTimeOffset From { get; set; }
        /// <summary>
    /// Gets or sets the To.
    /// </summary>
public required DateTimeOffset To { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.CreatedBy == CreatedBy &&
                        x.CreatedOn >= From &&
                        x.CreatedOn < To)
            .ToList();
    }
}

/// <summary>
/// Represents a class for EntitiesByModifiedByInDateRangeQuery.
/// </summary>
public abstract class EntitiesByModifiedByInDateRangeQuery<T> : EntitiesByModifiedByInDateRangeQuery<T, long>
    where T : global::Aero.Core.Entities.Entity;

/// <summary>
/// Represents a class for EntitiesByModifiedByInDateRangeQuery.
/// </summary>
public abstract class EntitiesByModifiedByInDateRangeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
        /// <summary>
    /// Gets or sets the Modified By.
    /// </summary>
public required string ModifiedBy { get; set; }
        /// <summary>
    /// Gets or sets the From.
    /// </summary>
public required DateTimeOffset From { get; set; }
        /// <summary>
    /// Gets or sets the To.
    /// </summary>
public required DateTimeOffset To { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.ModifiedOn != null &&
                        x.ModifiedBy == ModifiedBy &&
                        x.ModifiedOn >= From &&
                        x.ModifiedOn < To)
            .ToList();
    }
}


/// <summary>
/// Represents a class for LatestCreatedByQuery.
/// </summary>
public abstract class LatestCreatedByQuery<T> : LatestCreatedByQuery<T, long>
    where T : global::Aero.Core.Entities.Entity;

/// <summary>
/// Represents a class for LatestCreatedByQuery.
/// </summary>
public abstract class LatestCreatedByQuery<T, TKey> : ICompiledQuery<T, T?>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
        /// <summary>
    /// Gets or sets the Created By.
    /// </summary>
public required string CreatedBy { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public virtual Expression<Func<ISurrealDbQueryable<T>, T?>> QueryIs()
    {
        return q => q
            .Where(x => x.CreatedBy == CreatedBy)
            .OrderByDescending(x => x.CreatedOn)
            .FirstOrDefault();
    }
}

/// <summary>
/// Represents a class for LatestModifiedByQuery.
/// </summary>
public abstract class LatestModifiedByQuery<T> : LatestModifiedByQuery<T, long>
    where T : global::Aero.Core.Entities.Entity;

/// <summary>
/// Represents a class for LatestModifiedByQuery.
/// </summary>
public abstract class LatestModifiedByQuery<T, TKey> : ICompiledQuery<T, T?>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
        /// <summary>
    /// Gets or sets the Modified By.
    /// </summary>
public required string ModifiedBy { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public virtual Expression<Func<ISurrealDbQueryable<T>, T?>> QueryIs()
    {
        return q => q
            .Where(x => x.ModifiedOn != null && x.ModifiedBy == ModifiedBy)
            .OrderByDescending(x => x.ModifiedOn)
            .FirstOrDefault();
    }
}

/// <summary>
/// Represents a class for TouchedInRangeQuery.
/// </summary>
public abstract class TouchedInRangeQuery<T> : ICompiledQuery<T, IList<T>>
    where T : global::Aero.Core.Entities.Entity
    {
        /// <summary>
    /// Gets or sets the From.
    /// </summary>
public required DateTimeOffset From { get; set; }
        /// <summary>
    /// Gets or sets the To.
    /// </summary>
public required DateTimeOffset To { get; set; }

        /// <summary>
    /// QueryIs method.
    /// </summary>
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x =>
                (x.CreatedOn >= From && x.CreatedOn <= To) ||
                (x.ModifiedOn != null && x.ModifiedOn >= From && x.ModifiedOn <= To))
            .ToList();
    }
}

/// <summary>
/// Represents a class for EntitiesCreatedInRangePagedQuery.
/// </summary>
public abstract class EntitiesCreatedInRangePagedQuery<T> : ICompiledQuery<T, IList<T>>
    where T : global::Aero.Core.Entities.Entity
    {
        /// <summary>
    /// Gets or sets the From.
    /// </summary>
public required DateTimeOffset From { get; set; }
        /// <summary>
    /// Gets or sets the To.
    /// </summary>
public required DateTimeOffset To { get; set; }
        /// <summary>
    /// Gets or sets the Skip.
    /// </summary>
public int Skip { get; set; }
        /// <summary>
    /// Gets or sets the Take.
    /// </summary>
public int Take { get; set; } = 50;

        /// <summary>
    /// QueryIs method.
    /// </summary>
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.CreatedOn >= From && x.CreatedOn <= To)
            .OrderByDescending(x => x.CreatedOn)
            .Skip(Skip)
            .Take(Take)
            .ToList();
    }
}