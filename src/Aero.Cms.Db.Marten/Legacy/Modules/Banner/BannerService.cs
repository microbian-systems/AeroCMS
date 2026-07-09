using Aero.Marten;
using System.Linq.Expressions;
using Marten;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Banner;


public interface IBannerService : IGenericMartenRepository<BannerModel>
{
    public Task<IList<BannerModel>> FindByDateRange(DateTimeOffset start, DateTimeOffset end);
}


public class BannerService(IDocumentSession session, ILogger<BannerService> log)
    : GenericMartenRepository<BannerModel>(session, log), IBannerService
{
    public async Task<IList<BannerModel>> FindByDateRange(DateTimeOffset start, DateTimeOffset end)
    {
        Expression<Func<BannerModel, bool>> predicate = b => b.StartDate >= start && b.EndDate <= end;

        var results = await FindAsync(predicate);
        return results.ToList();
    }
}
