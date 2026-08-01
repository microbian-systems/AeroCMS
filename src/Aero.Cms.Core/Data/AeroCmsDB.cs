using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using static Aero.Core.Railway.Prelude;


namespace Aero.Cms.Core.Data;

/// <summary>
/// Provides a thin wrapper over a Sable document session for staging writes and executing
/// common queries.
/// </summary>
/// <remarks>
/// Mutation methods stage changes in <see cref="IDocumentSession"/> but do not call
/// <see cref="IDocumentSession.SaveChangesAsync(CancellationToken)"/>. Callers are responsible
/// for committing the session. This contract makes no thread-safety guarantee and does not
/// own or dispose the exposed session.
/// </remarks>
public interface IAeroCmsDb
{
    /// <summary>
    /// Gets the document session used for all wrapper operations.
    /// </summary>
    /// <remarks>The session's lifetime and concurrency rules are defined by its owner.</remarks>
    public IDocumentSession session { get; }

    /// <summary>
    /// Stages an entity for storage without committing the session.
    /// </summary>
    /// <typeparam name="T">The type of the entity to add.</typeparam>
    /// <param name="entity">The entity to store.</param>
    /// <returns>An already-completed task after the session's <c>Store</c> operation is called.</returns>
    public Task AddAsync<T>(T entity) where T : class;

    /// <summary>
    /// Stages an entity for deletion without committing the session.
    /// </summary>
    /// <typeparam name="T">The type of the entity to delete.</typeparam>
    /// <param name="entity">The entity to delete.</param>
    /// <returns>An already-completed task after the deletion is staged.</returns>
    public Task DeleteAsync<T>(T entity) where T : class;

    /// <summary>
    /// Stages an entity for storage without committing the session.
    /// </summary>
    /// <typeparam name="T">The type of the entity to update.</typeparam>
    /// <param name="entity">The entity to store.</param>
    /// <returns>An already-completed task after the session's <c>Store</c> operation is called.</returns>
    /// <remarks>This operation is identical to <see cref="AddAsync{T}(T)"/>.</remarks>
    public Task UpdateAsync<T>(T entity) where T : class;

    /// <summary>
    /// Loads a document by its numeric identifier.
    /// </summary>
    /// <typeparam name="T">The type of entity to retrieve.</typeparam>
    /// <param name="id">The document identifier.</param>
    /// <returns>An option containing the document, or an empty option when no document is found.</returns>
    /// <remarks>No cancellation token is accepted; session/provider exceptions propagate.</remarks>
    public Task<Option<T>> GetByIdAsync<T>(long id) where T : class;

    /// <summary>
    /// Loads documents whose identifiers occur in a sequence.
    /// </summary>
    /// <typeparam name="T">The type of entity to retrieve.</typeparam>
    /// <param name="ids">The document identifiers.</param>
    /// <returns>The documents returned by the session, or an empty sequence.</returns>
    /// <remarks>No cancellation token is accepted; session/provider exceptions propagate.</remarks>
    public Task<IEnumerable<T>> GetByIdsAsync<T>(IEnumerable<long> ids) where T : class;

    /// <summary>
    /// Determines whether any document satisfies a predicate.
    /// </summary>
    /// <typeparam name="T">The type of entity to evaluate.</typeparam>
    /// <param name="predicate">An expression that defines the condition to test for each entity.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains <see langword="true"/> if any
    /// entities satisfy the condition; otherwise, <see langword="false"/>.</returns>
    /// <remarks>No cancellation token is accepted; query/provider exceptions propagate.</remarks>
    public Task<bool> ExistsAsync<T>(Expression<Func<T, bool>> predicate) where T : class;

    /// <summary>
    /// Executes a paged query for documents that satisfy a predicate.
    /// </summary>
    /// <remarks>
    /// Paging uses <c>Skip((page - 1) * rows).Take(rows)</c>. The wrapper does not validate
    /// either argument and does not accept cancellation; provider failures propagate.
    /// </remarks>
    /// <typeparam name="T">The type of entity to search for.</typeparam>
    /// <param name="predicate">An expression that defines the conditions each entity must satisfy to be included in the result.</param>
    /// <param name="page">The one-based page number used in the skip calculation.</param>
    /// <param name="rows">The value passed to <c>Take</c>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable collection of entities
    /// that match the predicate for the specified page.</returns>
    public Task<IEnumerable<T>> FindAsync<T>(Expression<Func<T, bool>> predicate, int page=1, int rows=10) where T : class;
    /// <summary>
    /// Creates a deferred Sable query for a document type.
    /// </summary>
    /// <remarks>The query is not executed by this method and remains associated with the session.</remarks>
    /// <typeparam name="T">The type of entity to query.</typeparam>
    /// <returns>A queryable sequence that can be used to query entities of the requested type.</returns>
    public IQueryable<T> Query<T>() where T : class;
}

/// <summary>Implements <see cref="IAeroCmsDb"/> over a supplied Sable session.</summary>
/// <param name="sesh">The caller-owned document session.</param>
/// <param name="log">A logger accepted for compatibility; the current implementation does not use it.</param>
public class AeroCmsDB(IDocumentSession sesh, ILogger<AeroCmsDB> log) 
    : IAeroCmsDb
{
    /// <inheritdoc />
    public IDocumentSession session => sesh;

    /// <inheritdoc />
    public Task AddAsync<T>(T entity) where T : class
    {
        session.Store(entity);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync<T>(T entity) where T : class
    {
        session.Delete(entity);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync<T>(Expression<Func<T, bool>> predicate) where T : class
    {
        var exists = await session.Query<T>()
            .Where(predicate)
            .AnyAsync();
        return exists;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync<T>(Expression<Func<T, bool>> predicate, int page=1, int rows=10) where T : class
    {
        var documents = await session.Query<T>()
            .Where(predicate)
            .Skip((page - 1) * rows)
            .Take(rows)
            .ToListAsync();
        return documents ?? [];
    }

    /// <inheritdoc />
    public async Task<Option<T>> GetByIdAsync<T>(long id) where T : class
    {
        var document = await session.LoadAsync<T>(id);
        return document switch
        {
            null => None,
            _ => Some(document)
        };
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> GetByIdsAsync<T>(IEnumerable<long> ids) where T : class
    {
        var documents = await session.LoadManyAsync<T>(ids)
            ;
        return ((IEnumerable<T>)documents) ?? [];
    }

    /// <inheritdoc />
    public IQueryable<T> Query<T>() where T : class
    {
        return session.Query<T>();
    }

    /// <inheritdoc />
    public Task UpdateAsync<T>(T entity) where T : class
    {
        session.Store(entity);
        return Task.CompletedTask;
    }
}
