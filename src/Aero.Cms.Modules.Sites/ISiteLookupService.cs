using Aero.Cms.Abstractions.Models;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Provides read-only site views for host resolution and manager selection.
/// </summary>
public interface ISiteLookupService
{
    /// <summary>
    /// Returns every persisted site ordered by name, including disabled sites.
    /// </summary>
    /// <param name="cancellationToken">The token used by the backing site and host queries.</param>
    /// <returns>Site views enriched with all assigned host names.</returns>
Task<IReadOnlyList<SiteViewModel>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a host name to its enabled parent site.
    /// </summary>
    /// <param name="host">A host name, optionally including casing or formatting normalized by <c>HostNormalizer</c>.</param>
    /// <param name="cancellationToken">The token used by all backing queries.</param>
    /// <returns>
    /// The matched enabled site enriched with its hosts, or <see langword="null"/> when the host
    /// is unassigned, the parent site is missing, or the parent site is disabled.
    /// </returns>
Task<SiteViewModel?> ResolveByHostAsync(string host, CancellationToken cancellationToken = default);
}
