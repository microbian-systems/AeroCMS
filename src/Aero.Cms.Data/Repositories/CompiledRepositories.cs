using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Queries.Base;
using Aero.Core.Entities;
using Marten;
using Marten.Linq;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Aero.Cms.Data.Repositories;


public interface IMartenCompiledRepository<T, TKey>
    where T : class, IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    Task<T?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
    Task<IList<T>> GetByIdsAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Delete(T entity);
}

public interface IMartenCompiledRepository<T> : IMartenCompiledRepository<T, long>
    where T : Entity;


public abstract class MartenCompiledRepository<T>(IDocumentSession session) 
    : MartenCompiledRepository<T, long>(session), IMartenCompiledRepository<T>
    where T : Entity
{
}

public abstract class MartenCompiledRepository<T, TKey> : IMartenCompiledRepository<T, TKey>
    where T : class, IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>, IComparable<TKey>
{
    protected readonly IDocumentSession Session;

    protected MartenCompiledRepository(IDocumentSession session)
    {
        Session = session;
    }

    public virtual async Task<T?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var query = CreateByIdQuery(id);
        return await Session.QueryAsync(query, cancellationToken);
    }

    public virtual async Task<IList<T>> GetByIdsAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        var query = CreateByIdsQuery(ids);
        var results = await Session.QueryAsync(query, cancellationToken);
        return results;
    }

    public virtual Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        Session.Store(entity);
        return Task.CompletedTask;
    }

    public virtual void Update(T entity)
    {
        Session.Store(entity);
    }

    public virtual void Delete(T entity)
    {
        Session.Delete(entity);
    }

    protected virtual EntityByIdQuery<T, TKey> CreateByIdQuery(TKey id)
    {
        var query = new EntityByIdQuery<T, TKey>() { Id = id };
        return query;
    }

    protected virtual EntitiesByIdsQuery<T, TKey> CreateByIdsQuery(IEnumerable<TKey> ids)
    {
        var query = new EntitiesByIdsQuery<T, TKey>() { Ids = ids };
        return query;
    }
}

