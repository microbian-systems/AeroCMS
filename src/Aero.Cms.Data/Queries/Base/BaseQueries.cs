using Aero.Core.Entities;
using Marten;
using Marten.Linq;
using System.Linq.Expressions;

namespace Aero.Cms.Data.Queries.Base;

public class EntityByIdQuery<T> : EntityByIdQuery<T, long>
    where T : Entity;

public class EntityByIdQuery<T, TKey> : ICompiledQuery<T, T?>
    where T : IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    public required TKey Id { get; set; }

    public virtual Expression<Func<IMartenQueryable<T>, T?>> QueryIs()
    {
        return q => q.FirstOrDefault(x => x.Id.Equals(Id));
    }
}

public abstract class EntityByIdQueryList<T> : EntityByIdQueryList<T, long>
    where T : notnull, Entity;

public abstract class EntityByIdQueryList<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : notnull, Entity
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    public TKey Id { get; set; }

    public virtual Expression<Func<IMartenQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => x.Id.Equals(Id)).ToList();
    }
}

public class EntitiesByIdsQuery<T> : EntitiesByIdsQuery<T, long>
    where T : Entity;

public class EntitiesByIdsQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    public IEnumerable<TKey> Ids { get; init; } = [];
    public virtual Expression<Func<IMartenQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => x.Id.In(Ids.ToArray())).ToList();
    }
}

public abstract class EntitiesByCreatedByQuery<T> : EntitiesByCreatedByQuery<T, long>
    where T : Entity;

public abstract class EntitiesByCreatedByQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    public required string CreatedBy { get; set; }

    public virtual Expression<Func<IMartenQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => x.CreatedBy == CreatedBy).ToList();
    }
}

public abstract class EntitiesByModifiedByQuery<T> : EntitiesByModifiedByQuery<T, long>
    where T : Entity;

public abstract class EntitiesByModifiedByQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    public required string ModifiedBy { get; set; }

    public virtual Expression<Func<IMartenQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => x.ModifiedBy == ModifiedBy).ToList();
    }
}

public abstract class EntitiesCreatedInRangeQuery<T> : EntitiesCreatedInRangeQuery<T, long>
    where T : Entity;

public abstract class EntitiesCreatedInRangeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    public required DateTimeOffset From { get; set; }
    public required DateTimeOffset To { get; set; }

    public virtual Expression<Func<IMartenQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.CreatedOn >= From && x.CreatedOn < To)
            .ToList();
    }
}

public abstract class EntitiesModifiedInRangeQuery<T> : EntitiesModifiedInRangeQuery<T, long>
    where T : Entity;

public abstract class EntitiesModifiedInRangeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    public required DateTimeOffset From { get; set; }
    public required DateTimeOffset To { get; set; }

    public virtual Expression<Func<IMartenQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.ModifiedOn != null &&
                        x.ModifiedOn >= From &&
                        x.ModifiedOn < To)
            .ToList();
    }
}


public abstract class EntitiesCreatedSinceQuery<T> : EntitiesCreatedSinceQuery<T, long>
    where T : Entity;

public abstract class EntitiesCreatedSinceQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    public required DateTimeOffset Since { get; set; }

    public virtual Expression<Func<IMartenQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => x.CreatedOn >= Since).ToList();
    }
}

public abstract class EntitiesCreatedBeforeQuery<T> : EntitiesCreatedBeforeQuery<T, long>
    where T : Entity;

public abstract class EntitiesCreatedBeforeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    public required DateTimeOffset Before { get; set; }

    public virtual Expression<Func<IMartenQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q.Where(x => x.CreatedOn < Before).ToList();
    }
}

public abstract class EntitiesModifiedSinceQuery<T> : EntitiesModifiedSinceQuery<T, long>
    where T : Entity;

public abstract class EntitiesModifiedSinceQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    public required DateTimeOffset Since { get; set; }

    public virtual Expression<Func<IMartenQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.ModifiedOn != null && x.ModifiedOn >= Since)
            .ToList();
    }
}

public abstract class EntitiesModifiedBeforeQuery<T> : EntitiesModifiedBeforeQuery<T, long>
    where T : Entity;

public abstract class EntitiesModifiedBeforeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    public required DateTimeOffset Before { get; set; }

    public virtual Expression<Func<IMartenQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.ModifiedOn != null && x.ModifiedOn < Before)
            .ToList();
    }
}

public abstract class EntitiesByCreatedByInDateRangeQuery<T> : EntitiesByCreatedByInDateRangeQuery<T, long>
    where T : Entity;

public abstract class EntitiesByCreatedByInDateRangeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    public required string CreatedBy { get; set; }
    public required DateTimeOffset From { get; set; }
    public required DateTimeOffset To { get; set; }

    public virtual Expression<Func<IMartenQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.CreatedBy == CreatedBy &&
                        x.CreatedOn >= From &&
                        x.CreatedOn < To)
            .ToList();
    }
}

public abstract class EntitiesByModifiedByInDateRangeQuery<T> : EntitiesByModifiedByInDateRangeQuery<T, long>
    where T : Entity;

public abstract class EntitiesByModifiedByInDateRangeQuery<T, TKey> : ICompiledQuery<T, IList<T>>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    public required string ModifiedBy { get; set; }
    public required DateTimeOffset From { get; set; }
    public required DateTimeOffset To { get; set; }

    public virtual Expression<Func<IMartenQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.ModifiedOn != null &&
                        x.ModifiedBy == ModifiedBy &&
                        x.ModifiedOn >= From &&
                        x.ModifiedOn < To)
            .ToList();
    }
}


public abstract class LatestCreatedByQuery<T> : LatestCreatedByQuery<T, long>
    where T : Entity;

public abstract class LatestCreatedByQuery<T, TKey> : ICompiledQuery<T, T?>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    public required string CreatedBy { get; set; }

    public virtual Expression<Func<IMartenQueryable<T>, T?>> QueryIs()
    {
        return q => q
            .Where(x => x.CreatedBy == CreatedBy)
            .OrderByDescending(x => x.CreatedOn)
            .FirstOrDefault();
    }
}

public abstract class LatestModifiedByQuery<T> : LatestModifiedByQuery<T, long>
    where T : Entity;

public abstract class LatestModifiedByQuery<T, TKey> : ICompiledQuery<T, T?>
    where T : EntityBase<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    public required string ModifiedBy { get; set; }

    public virtual Expression<Func<IMartenQueryable<T>, T?>> QueryIs()
    {
        return q => q
            .Where(x => x.ModifiedOn != null && x.ModifiedBy == ModifiedBy)
            .OrderByDescending(x => x.ModifiedOn)
            .FirstOrDefault();
    }
}

public abstract class TouchedInRangeQuery<T> : ICompiledQuery<T, IList<T>>
    where T : Entity
{
    public required DateTimeOffset From { get; set; }
    public required DateTimeOffset To { get; set; }

    public virtual Expression<Func<IMartenQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x =>
                (x.CreatedOn >= From && x.CreatedOn <= To) ||
                (x.ModifiedOn != null && x.ModifiedOn >= From && x.ModifiedOn <= To))
            .ToList();
    }
}

public abstract class EntitiesCreatedInRangePagedQuery<T> : ICompiledQuery<T, IList<T>>
    where T : Entity
{
    public required DateTimeOffset From { get; set; }
    public required DateTimeOffset To { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 50;

    public virtual Expression<Func<IMartenQueryable<T>, IList<T>>> QueryIs()
    {
        return q => q
            .Where(x => x.CreatedOn >= From && x.CreatedOn <= To)
            .OrderByDescending(x => x.CreatedOn)
            .Skip(Skip)
            .Take(Take)
            .ToList();
    }
}