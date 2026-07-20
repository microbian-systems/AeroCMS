using System.Linq.Expressions;
using Aero.Cms.Core.Models;

namespace Aero.Cms.Modules.Media;

/// <summary>
/// Provides unscoped document-session access to media assets.
/// </summary>
/// <remarks>
/// Methods do not apply tenant or site filters. Callers are responsible for enforcing ownership
/// before reading or mutating an asset.
/// </remarks>
public interface IMediaRepository
{
    /// <summary>
    /// Loads a media asset by document identifier.
    /// </summary>
    /// <param name="id">The document identifier.</param>
    /// <param name="ct">Cancels the database operation.</param>
    /// <returns>The asset, or <see langword="null"/> when it does not exist.</returns>
Task<MediaAsset?> GetByIdAsync(long id, CancellationToken ct = default);
    /// <summary>
    /// Loads every media asset visible to the document session.
    /// </summary>
    /// <param name="ct">Cancels the database query.</param>
    /// <returns>A materialized collection of assets.</returns>
Task<IReadOnlyCollection<MediaAsset>> GetAllAsync(CancellationToken ct = default);
    /// <summary>
    /// Materializes the assets matching a provider-translatable predicate.
    /// </summary>
    /// <param name="predicate">The expression translated by the AeroDB query provider.</param>
    /// <param name="ct">Cancels the database query.</param>
    /// <returns>The matching assets.</returns>
Task<IEnumerable<MediaAsset>> FindAsync(Expression<Func<MediaAsset, bool>> predicate, CancellationToken ct = default);
    /// <summary>
    /// Counts every media asset visible to the document session.
    /// </summary>
    /// <param name="ct">Cancels the database query.</param>
    /// <returns>The total document count.</returns>
Task<int> CountAsync(CancellationToken ct = default);
    /// <summary>
    /// Stores an asset and immediately commits the session.
    /// </summary>
    /// <param name="entity">The caller-initialized asset to persist.</param>
    /// <param name="ct">Cancels the commit.</param>
    /// <returns>The same asset instance after a successful commit.</returns>
Task<MediaAsset> InsertAsync(MediaAsset entity, CancellationToken ct = default);
    /// <summary>
    /// Stores an asset and immediately commits the session.
    /// </summary>
    /// <param name="entity">The complete asset state to persist.</param>
    /// <param name="ct">Cancels the commit.</param>
    /// <returns>The same asset instance after a successful commit.</returns>
Task<MediaAsset> UpdateAsync(MediaAsset entity, CancellationToken ct = default);
    /// <summary>
    /// Deletes an asset by identifier and immediately commits the session.
    /// </summary>
    /// <param name="id">The document identifier to delete.</param>
    /// <param name="ct">Cancels the commit.</param>
    /// <returns>A task representing the delete and commit.</returns>
Task DeleteAsync(long id, CancellationToken ct = default);
    /// <summary>
    /// Deletes the supplied asset and immediately commits the session.
    /// </summary>
    /// <param name="entity">The asset to delete.</param>
    /// <param name="ct">Cancels the commit.</param>
    /// <returns>A task representing the delete and commit.</returns>
Task DeleteAsync(MediaAsset entity, CancellationToken ct = default);
}

/// <summary>
/// Implements direct media persistence over one scoped AeroDB document session.
/// </summary>
/// <param name="session">The session used for all queries, staging, and commits.</param>
/// <param name="logger">The logger for repository operations.</param>
public class MediaRepository(IDocumentSession session, ILogger<MediaRepository> logger) : IMediaRepository
{
    /// <inheritdoc />
public async Task<MediaAsset?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        logger.LogInformation("Fetching media asset with id {Id}", id);
        return await session.LoadAsync<MediaAsset>(id, ct);
    }

    /// <inheritdoc />
public async Task<IReadOnlyCollection<MediaAsset>> GetAllAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Fetching all media assets");
        return await session.Query<MediaAsset>().ToListAsync(ct);
    }

    /// <inheritdoc />
public async Task<IEnumerable<MediaAsset>> FindAsync(Expression<Func<MediaAsset, bool>> predicate, CancellationToken ct = default)
    {
        logger.LogInformation("Querying media assets with predicate");
        return await session.Query<MediaAsset>().Where(predicate).ToListAsync(ct);
    }

    /// <inheritdoc />
public async Task<int> CountAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Counting media assets");
        return await session.Query<MediaAsset>().CountAsync(ct);
    }

    /// <inheritdoc />
public async Task<MediaAsset> InsertAsync(MediaAsset entity, CancellationToken ct = default)
    {
        logger.LogInformation("Inserting media asset {FileName}", entity.FileName);
        session.Store(entity);
        await session.SaveChangesAsync(ct);
        return entity;
    }

    /// <inheritdoc />
public async Task<MediaAsset> UpdateAsync(MediaAsset entity, CancellationToken ct = default)
    {
        logger.LogInformation("Updating media asset {Id}", entity.Id);
        session.Store(entity);
        await session.SaveChangesAsync(ct);
        return entity;
    }

    /// <inheritdoc />
public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        logger.LogInformation("Deleting media asset with id {Id}", id);
        session.Delete<MediaAsset>(id);
        await session.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
public async Task DeleteAsync(MediaAsset entity, CancellationToken ct = default)
    {
        logger.LogInformation("Deleting media asset {Id}", entity.Id);
        session.Delete(entity);
        await session.SaveChangesAsync(ct);
    }
}
