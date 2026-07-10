using AeroDB;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Banner;

public interface IBannerService
{
    Task<IEnumerable<BannerModel>> GetAllAsync();
    Task<BannerModel?> GetByIdAsync(long id);
    Task<BannerModel> InsertAsync(BannerModel entity);
    Task<BannerModel> UpdateAsync(BannerModel entity);
    Task DeleteAsync(long id);
    Task<IEnumerable<BannerModel>> FindAsync(Expression<Func<BannerModel, bool>> predicate);
    Task<long> CountAsync();
    Task<IList<BannerModel>> FindByDateRange(DateTimeOffset start, DateTimeOffset end);
}

public class BannerService(IDocumentSession session, ILogger<BannerService> log) : IBannerService
{
    public Task<IEnumerable<BannerModel>> GetAllAsync() =>
        session.Query<BannerModel>().ToListAsync().ContinueWith(t => (IEnumerable<BannerModel>)t.Result);

    public Task<BannerModel?> GetByIdAsync(long id) =>
        session.LoadAsync<BannerModel>(id);

    public Task<BannerModel> InsertAsync(BannerModel entity)
    {
        session.Store(entity);
        return Task.FromResult(entity);
    }

    public async Task<BannerModel> UpdateAsync(BannerModel entity)
    {
        session.Store(entity);
        await session.SaveChangesAsync();
        return entity;
    }

    public Task DeleteAsync(long id)
    {
        session.Delete<BannerModel>(id);
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<BannerModel>> FindAsync(Expression<Func<BannerModel, bool>> predicate)
    {
        log.LogInformation("querying marten store...");
        return await session.Query<BannerModel>().Where(predicate).ToListAsync();
    }

    public async Task<long> CountAsync()
    {
        log.LogInformation("counting entities in marten store...");
        return await session.Query<BannerModel>().CountAsync();
    }

    public async Task<IList<BannerModel>> FindByDateRange(DateTimeOffset start, DateTimeOffset end)
    {
        Expression<Func<BannerModel, bool>> predicate = b => b.StartDate >= start && b.EndDate <= end;
        var results = await FindAsync(predicate);
        return results.ToList();
    }
}
