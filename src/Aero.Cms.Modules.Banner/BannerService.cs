using AeroDB.Sable;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Banner;

/// <summary>
/// Provides session-backed reads and staged writes for banner documents.
/// </summary>
/// <remarks>
/// This contract does not render banners, enforce tenant/site/culture scope, evaluate whether a banner is
/// currently eligible, or cache results. Cancellation is not exposed by the current operations.
/// </remarks>
public interface IBannerService
{
    /// <summary>Gets all banner documents without ordering or eligibility filtering.</summary>
    /// <returns>The documents returned by the backing query.</returns>
Task<IEnumerable<BannerModel>> GetAllAsync();
    /// <summary>Loads a banner by identifier.</summary>
    /// <param name="id">The banner identifier.</param>
    /// <returns>The matching banner, or <see langword="null"/> when it is absent.</returns>
Task<BannerModel?> GetByIdAsync(long id);
    /// <summary>Stages a banner for storage in the current document session.</summary>
    /// <param name="entity">The banner to stage.</param>
    /// <returns>The same banner after it has been staged; this method does not save the session.</returns>
Task<BannerModel> InsertAsync(BannerModel entity);
    /// <summary>Stores a banner and saves the current document session.</summary>
    /// <param name="entity">The banner to store.</param>
    /// <returns>The supplied banner after the save completes.</returns>
Task<BannerModel> UpdateAsync(BannerModel entity);
    /// <summary>Stages deletion of a banner identifier in the current document session.</summary>
    /// <param name="id">The banner identifier to delete.</param>
    /// <returns>A completed task after deletion is staged; this method does not save the session.</returns>
Task DeleteAsync(long id);
    /// <summary>Queries banners using the supplied predicate.</summary>
    /// <param name="predicate">The predicate applied by the backing query provider.</param>
    /// <returns>The matching banner documents.</returns>
Task<IEnumerable<BannerModel>> FindAsync(Expression<Func<BannerModel, bool>> predicate);
    /// <summary>Counts all banner documents.</summary>
    /// <returns>The total document count without eligibility filtering.</returns>
Task<long> CountAsync();
    /// <summary>Finds banners whose start is on or after <paramref name="start"/> and whose end is on or before <paramref name="end"/>.</summary>
    /// <param name="start">Inclusive lower bound for <see cref="BannerModel.StartDate"/>.</param>
    /// <param name="end">Inclusive upper bound for <see cref="BannerModel.EndDate"/>.</param>
    /// <returns>Banners fully contained in the supplied interval.</returns>
Task<IList<BannerModel>> FindByDateRange(DateTimeOffset start, DateTimeOffset end);
}

/// <summary>
/// Session-backed implementation of <see cref="IBannerService"/>.
/// </summary>
/// <param name="session">The document session used for reads and staged writes; its owner controls its lifecycle.</param>
/// <param name="log">Logger used for query and count diagnostics.</param>
public class BannerService(IDocumentSession session, ILogger<BannerService> log) : IBannerService
{
    /// <inheritdoc />
public Task<IEnumerable<BannerModel>> GetAllAsync() =>
        session.Query<BannerModel>().ToListAsync().ContinueWith(t => (IEnumerable<BannerModel>)t.Result);

    /// <inheritdoc />
public Task<BannerModel?> GetByIdAsync(long id) =>
        session.LoadAsync<BannerModel>(id);

    /// <inheritdoc />
public Task<BannerModel> InsertAsync(BannerModel entity)
    {
        session.Store(entity);
        return Task.FromResult(entity);
    }

    /// <inheritdoc />
public async Task<BannerModel> UpdateAsync(BannerModel entity)
    {
        session.Store(entity);
        await session.SaveChangesAsync();
        return entity;
    }

    /// <inheritdoc />
public Task DeleteAsync(long id)
    {
        session.Delete<BannerModel>(id);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
public async Task<IEnumerable<BannerModel>> FindAsync(Expression<Func<BannerModel, bool>> predicate)
    {
        log.LogInformation("querying marten store...");
        return await session.Query<BannerModel>().Where(predicate).ToListAsync();
    }

    /// <inheritdoc />
public async Task<long> CountAsync()
    {
        log.LogInformation("counting entities in marten store...");
        return await session.Query<BannerModel>().CountAsync();
    }

    /// <inheritdoc />
public async Task<IList<BannerModel>> FindByDateRange(DateTimeOffset start, DateTimeOffset end)
    {
        Expression<Func<BannerModel, bool>> predicate = b => b.StartDate >= start && b.EndDate <= end;
        var results = await FindAsync(predicate);
        return results.ToList();
    }
}
