using System.Linq.Expressions;
using Aero.Cms.Core.Models;

namespace Aero.Cms.Modules.Media;

public interface IMediaRepository
{
    Task<MediaAsset?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyCollection<MediaAsset>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<MediaAsset>> FindAsync(Expression<Func<MediaAsset, bool>> predicate, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<MediaAsset> InsertAsync(MediaAsset entity, CancellationToken ct = default);
    Task<MediaAsset> UpdateAsync(MediaAsset entity, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    Task DeleteAsync(MediaAsset entity, CancellationToken ct = default);
}

public class MediaRepository(IDocumentSession session, ILogger<MediaRepository> logger) : IMediaRepository
{
    public async Task<MediaAsset?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        logger.LogInformation("Fetching media asset with id {Id}", id);
        return await session.LoadAsync<MediaAsset>(id, ct);
    }

    public async Task<IReadOnlyCollection<MediaAsset>> GetAllAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Fetching all media assets");
        return await session.Query<MediaAsset>().ToListAsync(ct);
    }

    public async Task<IEnumerable<MediaAsset>> FindAsync(Expression<Func<MediaAsset, bool>> predicate, CancellationToken ct = default)
    {
        logger.LogInformation("Querying media assets with predicate");
        return await session.Query<MediaAsset>().Where(predicate).ToListAsync(ct);
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Counting media assets");
        return await session.Query<MediaAsset>().CountAsync(ct);
    }

    public async Task<MediaAsset> InsertAsync(MediaAsset entity, CancellationToken ct = default)
    {
        logger.LogInformation("Inserting media asset {FileName}", entity.FileName);
        session.Store(entity);
        await session.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<MediaAsset> UpdateAsync(MediaAsset entity, CancellationToken ct = default)
    {
        logger.LogInformation("Updating media asset {Id}", entity.Id);
        session.Store(entity);
        await session.SaveChangesAsync(ct);
        return entity;
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        logger.LogInformation("Deleting media asset with id {Id}", id);
        session.Delete<MediaAsset>(id);
        await session.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(MediaAsset entity, CancellationToken ct = default)
    {
        logger.LogInformation("Deleting media asset {Id}", entity.Id);
        session.Delete(entity);
        await session.SaveChangesAsync(ct);
    }
}
