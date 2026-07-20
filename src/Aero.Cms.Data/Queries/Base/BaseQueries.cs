using Aero.Core.Data;
using AeroDB.Sable;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries.Base;

/// <summary>
/// Selects the first document whose <see cref="ISableDocument{TKey}.Id"/> equals
/// the supplied <see cref="Id"/>.
/// </summary>
/// <typeparam name="T">The Sable document type to query.</typeparam>
/// <remarks>The query returns <see langword="null"/> when no matching document exists.</remarks>
public class EntityByIdQuery<T> : ICompiledQuery<T, T?>
    where T : class, ISableDocument<long>
{
    /// <summary>The exact document identifier to match.</summary>
public required long Id { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<T>, T?>> QueryIs()
    {
        return q => q.FirstOrDefault(x => x.Id == Id);
    }
}

/// <summary>
/// Selects documents whose <see cref="ISableDocument{TKey}.Id"/> equals
/// the supplied <see cref="Id"/>.
/// </summary>
/// <typeparam name="T">The Sable document type to query.</typeparam>
/// <remarks>The result is materialized as a list and is empty when no document matches.</remarks>
public abstract class EntityByIdQueryList<T> : ICompiledQuery<T, IList<T>>
    where T : class, ISableDocument<long>
{
    /// <summary>The exact document identifier to match.</summary>
public long Id { get; set; }

    /// <inheritdoc />
public Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => x.Id == Id).ToList();
    }
}

/// <inheritdoc cref="EntitiesByIdsQuery{T,TKey}"/>
public class EntitiesByIdsQuery<T> : EntitiesByIdsQuery<T, long>
    where T : class, ISableDocument<long>, IAuditable;

/// <summary>Selects documents whose identifiers occur in a supplied sequence.</summary>
/// <typeparam name="T">The Sable document type to query.</typeparam>
/// <typeparam name="TKey">The document identifier type.</typeparam>
/// <remarks>
/// Results are materialized as a list without an explicit ordering clause. An empty
/// identifier sequence produces an empty result.
/// </remarks>
public class EntitiesByIdsQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : class, ISableDocument<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    /// <summary>The identifier sequence used by the membership predicate.</summary>
public IEnumerable<TKey> Ids { get; init; } = [];
    /// <inheritdoc />
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => Ids.Contains(x.Id)).ToList();
    }
}

/// <inheritdoc cref="EntitiesByCreatedByQuery{T,TKey}"/>
public abstract class EntitiesByCreatedByQuery<T> : EntitiesByCreatedByQuery<T, long>
    where T : SableDocument, IAuditable;

/// <summary>Selects audited documents whose creator value exactly matches a supplied value.</summary>
/// <typeparam name="T">The audited Sable document type to query.</typeparam>
/// <typeparam name="TKey">The document identifier type.</typeparam>
/// <remarks>The expression performs no normalization and declares no result ordering.</remarks>
public abstract class EntitiesByCreatedByQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : SableDocument<TKey>, IAuditable
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    /// <summary>The creator value used by the equality predicate.</summary>
public required string CreatedBy { get; set; }

    /// <inheritdoc />
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => x.CreatedBy == CreatedBy).ToList();
    }
}

/// <inheritdoc cref="EntitiesByModifiedByQuery{T,TKey}"/>
public abstract class EntitiesByModifiedByQuery<T> : EntitiesByModifiedByQuery<T, long>
    where T : SableDocument, IAuditable;

/// <summary>Selects audited documents whose modifier value exactly matches a supplied value.</summary>
/// <typeparam name="T">The audited Sable document type to query.</typeparam>
/// <typeparam name="TKey">The document identifier type.</typeparam>
/// <remarks>The expression performs no normalization and declares no result ordering.</remarks>
public abstract class EntitiesByModifiedByQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : SableDocument<TKey>, IAuditable
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    /// <summary>The modifier value used by the equality predicate.</summary>
public required string ModifiedBy { get; set; }

    /// <inheritdoc />
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => x.ModifiedBy == ModifiedBy).ToList();
    }
}

/// <inheritdoc cref="EntitiesCreatedInRangeQuery{T,TKey}"/>
public abstract class EntitiesCreatedInRangeQuery<T> : EntitiesCreatedInRangeQuery<T, long>
    where T : SableDocument, IAuditable;

/// <summary>Selects audited documents created within a half-open time interval.</summary>
/// <typeparam name="T">The audited Sable document type to query.</typeparam>
/// <typeparam name="TKey">The document identifier type.</typeparam>
/// <remarks>The interval includes <see cref="From"/> and excludes <see cref="To"/>; results are not explicitly ordered.</remarks>
public abstract class EntitiesCreatedInRangeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : SableDocument<TKey>, IAuditable
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    /// <summary>The inclusive lower bound for <see cref="IAuditable.CreatedOn"/>.</summary>
public required DateTimeOffset From { get; set; }
    /// <summary>The exclusive upper bound for <see cref="IAuditable.CreatedOn"/>.</summary>
public required DateTimeOffset To { get; set; }

    /// <inheritdoc />
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.CreatedOn >= From && x.CreatedOn < To)
            .ToList();
    }
}

/// <inheritdoc cref="EntitiesModifiedInRangeQuery{T,TKey}"/>
public abstract class EntitiesModifiedInRangeQuery<T> : EntitiesModifiedInRangeQuery<T, long>
    where T : SableDocument, IAuditable;

/// <summary>Selects audited documents modified within a half-open time interval.</summary>
/// <typeparam name="T">The audited Sable document type to query.</typeparam>
/// <typeparam name="TKey">The document identifier type.</typeparam>
/// <remarks>
/// Documents without a modification timestamp are excluded. The interval includes
/// <see cref="From"/> and excludes <see cref="To"/>; results are not explicitly ordered.
/// </remarks>
public abstract class EntitiesModifiedInRangeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : SableDocument<TKey>, IAuditable
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    /// <summary>The inclusive lower bound for <see cref="IAuditable.ModifiedOn"/>.</summary>
public required DateTimeOffset From { get; set; }
    /// <summary>The exclusive upper bound for <see cref="IAuditable.ModifiedOn"/>.</summary>
public required DateTimeOffset To { get; set; }

    /// <inheritdoc />
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.ModifiedOn != null &&
                        x.ModifiedOn >= From &&
                        x.ModifiedOn < To)
            .ToList();
    }
}


/// <inheritdoc cref="EntitiesCreatedSinceQuery{T,TKey}"/>
public abstract class EntitiesCreatedSinceQuery<T> : EntitiesCreatedSinceQuery<T, long>
    where T : SableDocument, IAuditable;

/// <summary>Selects audited documents created at or after a timestamp.</summary>
/// <typeparam name="T">The audited Sable document type to query.</typeparam>
/// <typeparam name="TKey">The document identifier type.</typeparam>
/// <remarks>Results are not explicitly ordered.</remarks>
public abstract class EntitiesCreatedSinceQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : SableDocument<TKey>, IAuditable
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    /// <summary>The inclusive lower bound for <see cref="IAuditable.CreatedOn"/>.</summary>
public required DateTimeOffset Since { get; set; }

    /// <inheritdoc />
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => x.CreatedOn >= Since).ToList();
    }
}

/// <inheritdoc cref="EntitiesCreatedBeforeQuery{T,TKey}"/>
public abstract class EntitiesCreatedBeforeQuery<T> : EntitiesCreatedBeforeQuery<T, long>
    where T : SableDocument, IAuditable;

/// <summary>Selects audited documents created before a timestamp.</summary>
/// <typeparam name="T">The audited Sable document type to query.</typeparam>
/// <typeparam name="TKey">The document identifier type.</typeparam>
/// <remarks>Results are not explicitly ordered.</remarks>
public abstract class EntitiesCreatedBeforeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : SableDocument<TKey>, IAuditable
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    /// <summary>The exclusive upper bound for <see cref="IAuditable.CreatedOn"/>.</summary>
public required DateTimeOffset Before { get; set; }

    /// <inheritdoc />
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => x.CreatedOn < Before).ToList();
    }
}

/// <inheritdoc cref="EntitiesModifiedSinceQuery{T,TKey}"/>
public abstract class EntitiesModifiedSinceQuery<T> : EntitiesModifiedSinceQuery<T, long>
    where T : SableDocument, IAuditable;

/// <summary>Selects audited documents modified at or after a timestamp.</summary>
/// <typeparam name="T">The audited Sable document type to query.</typeparam>
/// <typeparam name="TKey">The document identifier type.</typeparam>
/// <remarks>Documents without a modification timestamp are excluded; results are not explicitly ordered.</remarks>
public abstract class EntitiesModifiedSinceQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : SableDocument<TKey>, IAuditable
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    /// <summary>The inclusive lower bound for <see cref="IAuditable.ModifiedOn"/>.</summary>
public required DateTimeOffset Since { get; set; }

    /// <inheritdoc />
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.ModifiedOn != null && x.ModifiedOn >= Since)
            .ToList();
    }
}

/// <inheritdoc cref="EntitiesModifiedBeforeQuery{T,TKey}"/>
public abstract class EntitiesModifiedBeforeQuery<T> : EntitiesModifiedBeforeQuery<T, long>
    where T : SableDocument, IAuditable;

/// <summary>Selects audited documents modified before a timestamp.</summary>
/// <typeparam name="T">The audited Sable document type to query.</typeparam>
/// <typeparam name="TKey">The document identifier type.</typeparam>
/// <remarks>Documents without a modification timestamp are excluded; results are not explicitly ordered.</remarks>
public abstract class EntitiesModifiedBeforeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : SableDocument<TKey>, IAuditable
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    /// <summary>The exclusive upper bound for <see cref="IAuditable.ModifiedOn"/>.</summary>
public required DateTimeOffset Before { get; set; }

    /// <inheritdoc />
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.ModifiedOn != null && x.ModifiedOn < Before)
            .ToList();
    }
}

/// <inheritdoc cref="EntitiesByCreatedByInDateRangeQuery{T,TKey}"/>
public abstract class EntitiesByCreatedByInDateRangeQuery<T> : EntitiesByCreatedByInDateRangeQuery<T, long>
    where T : SableDocument, IAuditable;

/// <summary>Selects documents created by an exact actor value within a half-open interval.</summary>
/// <typeparam name="T">The audited Sable document type to query.</typeparam>
/// <typeparam name="TKey">The document identifier type.</typeparam>
/// <remarks>The expression performs no actor normalization and declares no result ordering.</remarks>
public abstract class EntitiesByCreatedByInDateRangeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : SableDocument<TKey>, IAuditable
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    /// <summary>The creator value used by the equality predicate.</summary>
public required string CreatedBy { get; set; }
    /// <summary>The inclusive lower bound for <see cref="IAuditable.CreatedOn"/>.</summary>
public required DateTimeOffset From { get; set; }
    /// <summary>The exclusive upper bound for <see cref="IAuditable.CreatedOn"/>.</summary>
public required DateTimeOffset To { get; set; }

    /// <inheritdoc />
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.CreatedBy == CreatedBy &&
                        x.CreatedOn >= From &&
                        x.CreatedOn < To)
            .ToList();
    }
}

/// <inheritdoc cref="EntitiesByModifiedByInDateRangeQuery{T,TKey}"/>
public abstract class EntitiesByModifiedByInDateRangeQuery<T> : EntitiesByModifiedByInDateRangeQuery<T, long>
    where T : SableDocument, IAuditable;

/// <summary>Selects documents modified by an exact actor value within a half-open interval.</summary>
/// <typeparam name="T">The audited Sable document type to query.</typeparam>
/// <typeparam name="TKey">The document identifier type.</typeparam>
/// <remarks>Documents without a modification timestamp are excluded. The expression performs no actor normalization.</remarks>
public abstract class EntitiesByModifiedByInDateRangeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : SableDocument<TKey>, IAuditable
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    /// <summary>The modifier value used by the equality predicate.</summary>
public required string ModifiedBy { get; set; }
    /// <summary>The inclusive lower bound for <see cref="IAuditable.ModifiedOn"/>.</summary>
public required DateTimeOffset From { get; set; }
    /// <summary>The exclusive upper bound for <see cref="IAuditable.ModifiedOn"/>.</summary>
public required DateTimeOffset To { get; set; }

    /// <inheritdoc />
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


/// <inheritdoc cref="LatestCreatedByQuery{T,TKey}"/>
public abstract class LatestCreatedByQuery<T> : LatestCreatedByQuery<T, long>
    where T : SableDocument, IAuditable;

/// <summary>Selects the most recently created document for an exact creator value.</summary>
/// <typeparam name="T">The audited Sable document type to query.</typeparam>
/// <typeparam name="TKey">The document identifier type.</typeparam>
/// <remarks>Returns <see langword="null"/> when no document matches.</remarks>
public abstract class LatestCreatedByQuery<T, TKey> : ICompiledQuery<T, T?>
    where T : SableDocument<TKey>, IAuditable
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    /// <summary>The creator value used by the equality predicate.</summary>
public required string CreatedBy { get; set; }

    /// <inheritdoc />
public virtual Expression<Func<ISurrealDbQueryable<T>, T?>> QueryIs()
    {
        return q => q
            .Where(x => x.CreatedBy == CreatedBy)
            .OrderByDescending(x => x.CreatedOn)
            .FirstOrDefault();
    }
}

/// <inheritdoc cref="LatestModifiedByQuery{T,TKey}"/>
public abstract class LatestModifiedByQuery<T> : LatestModifiedByQuery<T, long>
    where T : SableDocument, IAuditable;

/// <summary>Selects the most recently modified document for an exact modifier value.</summary>
/// <typeparam name="T">The audited Sable document type to query.</typeparam>
/// <typeparam name="TKey">The document identifier type.</typeparam>
/// <remarks>Documents without a modification timestamp are excluded. Returns <see langword="null"/> when no document matches.</remarks>
public abstract class LatestModifiedByQuery<T, TKey> : ICompiledQuery<T, T?>
    where T : SableDocument<TKey>, IAuditable
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    /// <summary>The modifier value used by the equality predicate.</summary>
public required string ModifiedBy { get; set; }

    /// <inheritdoc />
public virtual Expression<Func<ISurrealDbQueryable<T>, T?>> QueryIs()
    {
        return q => q
            .Where(x => x.ModifiedOn != null && x.ModifiedBy == ModifiedBy)
            .OrderByDescending(x => x.ModifiedOn)
            .FirstOrDefault();
    }
}

/// <summary>Selects audited documents created or modified within an inclusive interval.</summary>
/// <typeparam name="T">The audited document type to query.</typeparam>
/// <remarks>Results are not explicitly ordered.</remarks>
public abstract class TouchedInRangeQuery<T> : ICompiledQuery<T, IList<T>>
    where T : class, IAuditable
    {
    /// <summary>The inclusive lower bound for creation or modification.</summary>
public required DateTimeOffset From { get; set; }
    /// <summary>The inclusive upper bound for creation or modification.</summary>
public required DateTimeOffset To { get; set; }

    /// <inheritdoc />
public virtual Expression<Func<ISurrealDbQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x =>
                (x.CreatedOn >= From && x.CreatedOn <= To) ||
                (x.ModifiedOn != null && x.ModifiedOn >= From && x.ModifiedOn <= To))
            .ToList();
    }
}

/// <summary>Selects one page of audited documents created within an inclusive interval.</summary>
/// <typeparam name="T">The audited document type to query.</typeparam>
/// <remarks>Results are ordered by creation time descending before <see cref="Skip"/> and <see cref="Take"/> are applied.</remarks>
public abstract class EntitiesCreatedInRangePagedQuery<T> : ICompiledQuery<T, IList<T>>
    where T : class, IAuditable
    {
    /// <summary>The inclusive lower bound for <see cref="IAuditable.CreatedOn"/>.</summary>
public required DateTimeOffset From { get; set; }
    /// <summary>The inclusive upper bound for <see cref="IAuditable.CreatedOn"/>.</summary>
public required DateTimeOffset To { get; set; }
    /// <summary>The number of ordered matches to skip.</summary>
public int Skip { get; set; }
    /// <summary>The maximum number of matches to return. The default is 50.</summary>
public int Take { get; set; } = 50;

    /// <inheritdoc />
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
