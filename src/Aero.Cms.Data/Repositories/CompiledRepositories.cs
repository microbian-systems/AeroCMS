using Aero.Core.Data;
using Aero.Cms.Data.Queries.Base;
using AeroDB.Sable;

namespace Aero.Cms.Data.Repositories;


/// <summary>
/// Defines an interface for IAeroCompiledRepository.
/// </summary>
public interface IAeroCompiledRepository<T>
    where T : class, ISableDocument<long>, IAuditable
{
        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetByIdsAsync method.
    /// </summary>
Task<IList<T>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default);

        /// <summary>
    /// AddAsync method.
    /// </summary>
Task AddAsync(T entity, CancellationToken cancellationToken = default);
        /// <summary>
    /// Update method.
    /// </summary>
void Update(T entity);
        /// <summary>
    /// Delete method.
    /// </summary>
void Delete(T entity);
}


/// <summary>
/// Represents a class for AeroCompiledRepository.
/// </summary>
public abstract class AeroCompiledRepository<T>(IDocumentSession session) : IAeroCompiledRepository<T>
    where T : class, ISableDocument<long>, IAuditable
{
        /// <summary>
    /// Session.
    /// </summary>
protected readonly IDocumentSession Session = session;

        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
public virtual async Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await Session.LoadAsync<T>(id, cancellationToken);
    }

        /// <summary>
    /// GetByIdsAsync method.
    /// </summary>
public virtual async Task<IList<T>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default)
    {
        var query = new EntitiesByIdsQuery<T> { Ids = ids };
        return await Session.QueryAsync(query, cancellationToken);
    }

        /// <summary>
    /// AddAsync method.
    /// </summary>
public virtual Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        Session.Store(entity);
        return Task.CompletedTask;
    }

        /// <summary>
    /// Update method.
    /// </summary>
public virtual void Update(T entity)
    {
        Session.Store(entity);
    }

        /// <summary>
    /// Delete method.
    /// </summary>
public virtual void Delete(T entity)
    {
        Session.Delete(entity);
    }

        /// <summary>
    /// CreateByIdQuery method.
    /// </summary>
protected virtual EntityByIdQuery<T> CreateByIdQuery(long id)
    {
        return new EntityByIdQuery<T> { Id = id };
    }

        /// <summary>
    /// CreateByIdsQuery method.
    /// </summary>
protected virtual EntitiesByIdsQuery<T> CreateByIdsQuery(IEnumerable<long> ids)
    {
        return new EntitiesByIdsQuery<T> { Ids = ids };
    }
}

