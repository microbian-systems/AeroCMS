using Aero.Cms.Abstractions.Models;

namespace Aero.Cms.Modules.Sites
{
    public interface ISiteLookupService
    {
        Task<IReadOnlyList<SiteViewModel>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<SiteViewModel?> ResolveByHostAsync(string host, CancellationToken cancellationToken = default);
    }
}