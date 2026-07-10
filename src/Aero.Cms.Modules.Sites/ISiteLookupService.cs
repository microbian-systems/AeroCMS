using Aero.Cms.Abstractions.Models;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Service for looking up sites by hostname. Used by <see cref="SiteResolutionMiddleware"/>.
/// </summary>
public interface ISiteLookupService
{
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
Task<IReadOnlyList<SiteViewModel>> GetAllAsync(CancellationToken cancellationToken = default);
        /// <summary>
    /// ResolveByHostAsync method.
    /// </summary>
Task<SiteViewModel?> ResolveByHostAsync(string host, CancellationToken cancellationToken = default);
}
