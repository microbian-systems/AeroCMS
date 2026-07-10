using System.Linq.Expressions;
using Aero.Cms.Core.Models;

namespace Aero.Cms.Modules.Media;

/// <summary>
/// Defines an interface for IMediaRepository.
/// </summary>
public interface IMediaRepository
{
        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
Task<MediaAsset?> GetByIdAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
Task<IReadOnlyCollection<MediaAsset>> GetAllAsync(CancellationToken ct = default);
        /// <summary>
    /// FindAsync method.
    /// </summary>
Task<IEnumerable<MediaAsset>> FindAsync(Expression<Func<MediaAsset, bool>> predicate, CancellationToken ct = default);
        /// <summary>
    /// CountAsync method.
    /// </summary>
Task<int> CountAsync(CancellationToken ct = default);
        /// <summary>
    /// InsertAsync method.
    /// </summary>
Task<MediaAsset> InsertAsync(MediaAsset entity, CancellationToken ct = default);
        /// <summary>
    /// UpdateAsync method.
    /// </summary>
Task<MediaAsset> UpdateAsync(MediaAsset entity, CancellationToken ct = default);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task DeleteAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task DeleteAsync(MediaAsset entity, CancellationToken ct = default);
}

/// <summary>
/// Represents a class for MediaRepository.
/// </summary>
public class MediaRepository(IDocumentSession session, ILogger<MediaRepository> logger) : IMediaRepository
{
        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
public async Task<MediaAsset?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        logger.LogInformation("Fetching media asset with id {Id}", id);
        return await session.LoadAsync<MediaAsset>(id, ct);
    }

        /// <summary>
    /// GetAllAsync method.
    /// </summary>
public async Task<IReadOnlyCollection<MediaAsset>> GetAllAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Fetching all media assets");
        return await session.Query<MediaAsset>().ToListAsync(ct);
    }

        /// <summary>
    /// FindAsync method.
    /// </summary>
public async Task<IEnumerable<MediaAsset>> FindAsync(Expression<Func<MediaAsset, bool>> predicate, CancellationToken ct = default)
    {
        logger.LogInformation("Querying media assets with predicate");
        return await session.Query<MediaAsset>().Where(predicate).ToListAsync(ct);
    }

        /// <summary>
    /// CountAsync method.
    /// </summary>
public async Task<int> CountAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Counting media assets");
        return await session.Query<MediaAsset>().CountAsync(ct);
    }

        /// <summary>
    /// InsertAsync method.
    /// </summary>
public async Task<MediaAsset> InsertAsync(MediaAsset entity, CancellationToken ct = default)
    {
        logger.LogInformation("Inserting media asset {FileName}", entity.FileName);
        session.Store(entity);
        await session.SaveChangesAsync(ct);
        return entity;
    }

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
public async Task<MediaAsset> UpdateAsync(MediaAsset entity, CancellationToken ct = default)
    {
        logger.LogInformation("Updating media asset {Id}", entity.Id);
        session.Store(entity);
        await session.SaveChangesAsync(ct);
        return entity;
    }

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        logger.LogInformation("Deleting media asset with id {Id}", id);
        session.Delete<MediaAsset>(id);
        await session.SaveChangesAsync(ct);
    }

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public async Task DeleteAsync(MediaAsset entity, CancellationToken ct = default)
    {
        logger.LogInformation("Deleting media asset {Id}", entity.Id);
        session.Delete(entity);
        await session.SaveChangesAsync(ct);
    }
}
