using Aero.Core.Entities;
using Aero.Core.Railway;
using Marten;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using Aero.Marten;
using static Aero.Core.Railway.Prelude;


namespace Aero.Cms.Core.Data;

/// <summary>
/// Defines the contract for a document-oriented database context that supports session-based operations, entity
/// persistence, and querying capabilities.
/// </summary>
/// <remarks>This interface extends the base database abstraction to provide document session management and
/// common CRUD operations for entities. Implementations are expected to manage the lifecycle of the underlying session
/// and ensure thread safety as appropriate. The generic methods are constrained to types derived from Entity, ensuring
/// consistent behavior across domain models.</remarks>
public interface IAeroCmsDb : IAeroDb
{
    /// <summary>
    /// Gets the current document session used for database operations.
    /// </summary>
    /// <remarks>The session provides access to querying, storing, and managing documents within the
    /// underlying data store. The lifetime and thread safety of the session depend on the implementation of the
    /// IDocumentSession interface.</remarks>
    public IDocumentSession session { get; }
    /// <summary>
    /// Asynchronously adds the specified entity to the data store.
    /// </summary>
    /// <typeparam name="T">The type of the entity to add. Must inherit from Entity.</typeparam>
    /// <param name="entity">The entity instance to add to the data store. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous add operation.</returns>
    public Task AddAsync<T>(T entity) where T : Entity;
    /// <summary>
    /// Asynchronously deletes the specified entity from the data store.
    /// </summary>
    /// <typeparam name="T">The type of the entity to delete. Must inherit from Entity.</typeparam>
    /// <param name="entity">The entity instance to delete. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public Task DeleteAsync<T>(T entity) where T : Entity;
    /// <summary>
    /// Asynchronously updates the specified entity in the data store.
    /// </summary>
    /// <typeparam name="T">The type of the entity to update. Must inherit from Entity.</typeparam>
    /// <param name="entity">The entity instance to update. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    public Task UpdateAsync<T>(T entity) where T : Entity;
    /// <summary>
    /// Asynchronously retrieves an entity of the specified type by its unique identifier.
    /// </summary>
    /// <typeparam name="T">The type of entity to retrieve. Must inherit from Entity.</typeparam>
    /// <param name="id">The unique identifier of the entity to retrieve.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an Option<T> with the entity if
    /// found; otherwise, an empty Option<T>.</returns>
    public Task<Option<T>> GetByIdAsync<T>(long id) where T : Entity;
    /// <summary>
    /// Asynchronously retrieves a collection of entities of type T that match the specified identifiers.
    /// </summary>
    /// <typeparam name="T">The type of entity to retrieve. Must inherit from Entity.</typeparam>
    /// <param name="ids">A collection of unique identifiers for the entities to retrieve. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable collection of entities
    /// of type T that correspond to the specified identifiers. If no entities are found, the collection is empty.</returns>
    public Task<IEnumerable<T>> GetByIdsAsync<T>(IEnumerable<long> ids) where T : Entity;
    /// <summary>
    /// Asynchronously determines whether any entities of type T satisfy the specified condition.
    /// </summary>
    /// <typeparam name="T">The type of entity to evaluate. Must inherit from Entity.</typeparam>
    /// <param name="predicate">An expression that defines the condition to test for each entity.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains <see langword="true"/> if any
    /// entities satisfy the condition; otherwise, <see langword="false"/>.</returns>
    public Task<bool> ExistsAsync<T>(Expression<Func<T, bool>> predicate) where T : Entity;
    /// <summary>
    /// Asynchronously retrieves a collection of entities that satisfy the specified predicate, with support for paging.
    /// </summary>
    /// <remarks>Paging is zero-based; specifying a higher page number skips more results. The method returns
    /// up to the specified number of entities per page, or fewer if there are not enough matching entities.</remarks>
    /// <typeparam name="T">The type of entity to search for. Must inherit from Entity.</typeparam>
    /// <param name="predicate">An expression that defines the conditions each entity must satisfy to be included in the result.</param>
    /// <param name="page">The page number of results to retrieve. Must be greater than or equal to 1.</param>
    /// <param name="rows">The maximum number of entities to return per page. Must be greater than 0.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable collection of entities
    /// that match the predicate for the specified page.</returns>
    public Task<IEnumerable<T>> FindAsync<T>(Expression<Func<T, bool>> predicate, int page=1, int rows=10) where T : Entity;
    /// <summary>
    /// Creates a queryable collection of entities of the specified type for LINQ operations.
    /// </summary>
    /// <remarks>The returned IQueryable<T> supports deferred execution and can be further filtered or
    /// projected using standard LINQ methods.</remarks>
    /// <typeparam name="T">The type of entity to query. Must inherit from Entity.</typeparam>
    /// <returns>An IQueryable<T> that can be used to query entities of type T.</returns>
    public IQueryable<T> Query<T>() where T : Entity;
}

/// <inheritdoc />
public class AeroCmsDB(IDocumentSession sesh, ILogger<AeroCmsDB> log) 
    : AeroDb(sesh, log), IAeroCmsDb
{
    /// <inheritdoc />
    public IDocumentSession session => sesh;

    /// <inheritdoc />
    public Task AddAsync<T>(T entity) where T : Entity
    {
        session.Store(entity);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync<T>(T entity) where T : Entity
    {
        session.Delete(entity);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync<T>(Expression<Func<T, bool>> predicate) where T : Entity
    {
        var exists = await session.Query<T>()
            .Take(1)
            .AnyAsync(predicate)
           ;
        return exists;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync<T>(Expression<Func<T, bool>> predicate, int page=1, int rows=10) where T : Entity
    {
        var documents = await session.Query<T>()
            .Where(predicate)
            .Skip((page - 1) * rows)
            .Take(rows)
            .ToListAsync();
        return documents ?? [];
    }

    /// <inheritdoc />
    public async Task<Option<T>> GetByIdAsync<T>(long id) where T : Entity
    {
        var document = await session.LoadAsync<T>(id);
        return document switch
        {
            null => None,
            _ => Some(document)
        };
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> GetByIdsAsync<T>(IEnumerable<long> ids) where T : Entity
    {
        var documents = await session.LoadManyAsync<T>(ids)
            ;
        return ((IEnumerable<T>)documents) ?? [];
    }

    /// <inheritdoc />
    public IQueryable<T> Query<T>() where T : Entity
    {
        return session.Query<T>();
    }

    /// <inheritdoc />
    public Task UpdateAsync<T>(T entity) where T : Entity
    {
        session.Store(entity);
        return Task.CompletedTask;
    }
}
