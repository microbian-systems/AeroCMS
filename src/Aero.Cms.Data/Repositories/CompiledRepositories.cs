using Aero.Core.Data;
using Aero.Cms.Data.Queries.Base;
using AeroDB.Sable;

namespace Aero.Cms.Data.Repositories;


/// <summary>
/// Defines the session-scoped persistence operations shared by repositories that
/// execute Sable compiled queries for audited documents.
/// </summary>
/// <typeparam name="T">The audited Sable document type handled by the repository.</typeparam>
/// <remarks>
/// Write operations stage changes in the repository's document session; callers
/// remain responsible for the session lifecycle and any subsequent persistence
/// boundary. Exceptions raised by the underlying session are not translated.
/// </remarks>
public interface IAeroCompiledRepository<T>
    where T : class, ISableDocument<long>, IAuditable
{
    /// <summary>
    /// Loads the document with the specified identifier.
    /// </summary>
    /// <param name="id">The document identifier.</param>
    /// <param name="cancellationToken">Token forwarded to the underlying read operation.</param>
    /// <returns>The matching document, or <see langword="null"/> when no document is found.</returns>
    /// <exception cref="OperationCanceledException">The read is canceled.</exception>
Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    /// <summary>
    /// Queries for documents whose identifiers are contained in the supplied sequence.
    /// </summary>
    /// <param name="ids">Document identifiers to query for.</param>
    /// <param name="cancellationToken">Token forwarded to the underlying query.</param>
    /// <returns>The documents returned by the compiled identifier query.</returns>
    /// <exception cref="OperationCanceledException">The query is canceled.</exception>
Task<IList<T>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a document for insertion in the current document session.
    /// </summary>
    /// <param name="entity">The document to stage.</param>
    /// <param name="cancellationToken">Reserved for the asynchronous contract; the current staging operation is synchronous.</param>
    /// <returns>A completed task after the document has been staged.</returns>
Task AddAsync(T entity, CancellationToken cancellationToken = default);
    /// <summary>
    /// Stages the supplied document for storage in the current document session.
    /// </summary>
    /// <param name="entity">The document to stage.</param>
    /// <remarks>This operation does not save or commit the session.</remarks>
void Update(T entity);
    /// <summary>
    /// Stages the supplied document for deletion in the current document session.
    /// </summary>
    /// <param name="entity">The document to stage for deletion.</param>
    /// <remarks>This operation does not save or commit the session.</remarks>
void Delete(T entity);
}


/// <summary>
/// Base implementation of <see cref="IAeroCompiledRepository{T}"/> backed by one document session.
/// </summary>
/// <typeparam name="T">The audited Sable document type handled by the repository.</typeparam>
/// <param name="session">The session used for all reads and staged writes. Its owner controls disposal and persistence.</param>
public abstract class AeroCompiledRepository<T>(IDocumentSession session) : IAeroCompiledRepository<T>
    where T : class, ISableDocument<long>, IAuditable
{
    /// <summary>The session shared by all operations on this repository instance.</summary>
protected readonly IDocumentSession Session = session;

    /// <inheritdoc />
public virtual async Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await Session.LoadAsync<T>(id, cancellationToken);
    }

    /// <inheritdoc />
public virtual async Task<IList<T>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default)
    {
        var query = new EntitiesByIdsQuery<T> { Ids = ids };
        return await Session.QueryAsync(query, cancellationToken);
    }

    /// <inheritdoc />
public virtual Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        Session.Store(entity);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
public virtual void Update(T entity)
    {
        Session.Store(entity);
    }

    /// <inheritdoc />
public virtual void Delete(T entity)
    {
        Session.Delete(entity);
    }

    /// <summary>
    /// Retains a factory for an identifier-query descriptor in the protected type
    /// surface.
    /// </summary>
    /// <param name="id">The document identifier to embed in the query descriptor.</param>
    /// <returns>A query descriptor that returns the matching document or <see langword="null"/>.</returns>
    /// <remarks>
    /// No current repository operation invokes this factory:
    /// <see cref="GetByIdAsync(long, CancellationToken)"/> calls
    /// the <see cref="IDocumentSession"/> <c>LoadAsync</c> operation directly.
    /// The presence of this unused member does not guarantee future invocation.
    /// </remarks>
protected virtual EntityByIdQuery<T> CreateByIdQuery(long id)
    {
        return new EntityByIdQuery<T> { Id = id };
    }

    /// <summary>
    /// Retains a factory for a multiple-identifier query descriptor in the
    /// protected type surface.
    /// </summary>
    /// <param name="ids">The identifier sequence to embed in the query descriptor.</param>
    /// <returns>A query descriptor that materializes matching documents as a list.</returns>
    /// <remarks>
    /// No current repository operation invokes this factory:
    /// <see cref="GetByIdsAsync(IEnumerable{long}, CancellationToken)"/> constructs
    /// <see cref="EntitiesByIdsQuery{T}"/> directly. The presence of this unused
    /// member does not guarantee future invocation.
    /// </remarks>
protected virtual EntitiesByIdsQuery<T> CreateByIdsQuery(IEnumerable<long> ids)
    {
        return new EntitiesByIdsQuery<T> { Ids = ids };
    }
}

