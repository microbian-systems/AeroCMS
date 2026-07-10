using Aero.Marten;
using System.Linq.Expressions;
using Marten;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Banner;


/// <summary>
/// Defines an interface for IBannerService.
/// </summary>
public interface IBannerService : IGenericMartenRepository<BannerModel>
{
        /// <summary>
    /// FindByDateRange method.
    /// </summary>
public Task<IList<BannerModel>> FindByDateRange(DateTimeOffset start, DateTimeOffset end);
}


/// <summary>
/// Represents a class for BannerService.
/// </summary>
public class BannerService(IDocumentSession session, ILogger<BannerService> log)
    : GenericMartenRepository<BannerModel>(session, log), IBannerService
{
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
