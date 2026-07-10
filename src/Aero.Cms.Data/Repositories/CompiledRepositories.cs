using Aero.Cms.Data.Queries.Base;
using Aero.Core.Entities;
using AeroDB;

namespace Aero.Cms.Data.Repositories;


public interface IAeroCompiledRepository<T>
    where T : global::Aero.Core.Entities.IEntity<long>
{
    Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IList<T>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Delete(T entity);
}


public abstract class AeroCompiledRepository<T>(IDocumentSession session) : IAeroCompiledRepository<T>
    where T : global::Aero.Core.Entities.Entity
{
    protected readonly IDocumentSession Session = session;

    public virtual async Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await Session.LoadAsync<T>(id, cancellationToken);
    }

    public virtual async Task<IList<T>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default)
    {
        var query = new EntitiesByIdsQuery<T> { Ids = ids };
        return await Session.QueryAsync(query, cancellationToken);
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

    protected virtual EntityByIdQuery<T> CreateByIdQuery(long id)
    {
        return new EntityByIdQuery<T> { Id = id };
    }

    protected virtual EntitiesByIdsQuery<T> CreateByIdsQuery(IEnumerable<long> ids)
    {
        return new EntitiesByIdsQuery<T> { Ids = ids };
    }
}

