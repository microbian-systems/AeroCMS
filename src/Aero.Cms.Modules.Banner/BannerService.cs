using AeroDB.Sable;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Banner;

/// <summary>
/// Defines an interface for IBannerService.
/// </summary>
public interface IBannerService
{
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
Task<IEnumerable<BannerModel>> GetAllAsync();
        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
Task<BannerModel?> GetByIdAsync(long id);
        /// <summary>
    /// InsertAsync method.
    /// </summary>
Task<BannerModel> InsertAsync(BannerModel entity);
        /// <summary>
    /// UpdateAsync method.
    /// </summary>
Task<BannerModel> UpdateAsync(BannerModel entity);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task DeleteAsync(long id);
        /// <summary>
    /// FindAsync method.
    /// </summary>
Task<IEnumerable<BannerModel>> FindAsync(Expression<Func<BannerModel, bool>> predicate);
        /// <summary>
    /// CountAsync method.
    /// </summary>
Task<long> CountAsync();
        /// <summary>
    /// FindByDateRange method.
    /// </summary>
Task<IList<BannerModel>> FindByDateRange(DateTimeOffset start, DateTimeOffset end);
}

/// <summary>
/// Represents a class for BannerService.
/// </summary>
public class BannerService(IDocumentSession session, ILogger<BannerService> log) : IBannerService
{
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
public Task<IEnumerable<BannerModel>> GetAllAsync() =>
        session.Query<BannerModel>().ToListAsync().ContinueWith(t => (IEnumerable<BannerModel>)t.Result);

        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
public Task<BannerModel?> GetByIdAsync(long id) =>
        session.LoadAsync<BannerModel>(id);

        /// <summary>
    /// InsertAsync method.
    /// </summary>
public Task<BannerModel> InsertAsync(BannerModel entity)
    {
        session.Store(entity);
        return Task.FromResult(entity);
    }

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
public async Task<BannerModel> UpdateAsync(BannerModel entity)
    {
        session.Store(entity);
        await session.SaveChangesAsync();
        return entity;
    }

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public Task DeleteAsync(long id)
    {
        session.Delete<BannerModel>(id);
        return Task.CompletedTask;
    }

        /// <summary>
    /// FindAsync method.
    /// </summary>
public async Task<IEnumerable<BannerModel>> FindAsync(Expression<Func<BannerModel, bool>> predicate)
    {
        log.LogInformation("querying marten store...");
        return await session.Query<BannerModel>().Where(predicate).ToListAsync();
    }

        /// <summary>
    /// CountAsync method.
    /// </summary>
public async Task<long> CountAsync()
    {
        log.LogInformation("counting entities in marten store...");
        return await session.Query<BannerModel>().CountAsync();
    }

        /// <summary>
    /// FindByDateRange method.
    /// </summary>
public async Task<IList<BannerModel>> FindByDateRange(DateTimeOffset start, DateTimeOffset end)
    {
        Expression<Func<BannerModel, bool>> predicate = b => b.StartDate >= start && b.EndDate <= end;
        var results = await FindAsync(predicate);
        return results.ToList();
    }
}
