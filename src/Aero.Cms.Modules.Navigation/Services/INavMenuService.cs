using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Modules.Navigation.Domain;

namespace Aero.Cms.Modules.Navigation.Services;

/// <summary>
/// Defines an interface for INavMenuService.
/// </summary>
public interface INavMenuService
{
        /// <summary>
    /// ListAsync method.
    /// </summary>
Task<Result<(IReadOnlyList<NavMenuDocument> Items, long TotalCount), AeroError>> ListAsync(
        int skip = 0,
        int take = 20,
        string? search = null,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// GetAsync method.
    /// </summary>
Task<Result<NavMenuDocument, AeroError>> GetAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
    /// GetDetailAsync method.
    /// </summary>
Task<Result<NavigationDetail, AeroError>> GetDetailAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
    /// ListCultureVariantsAsync method.
    /// </summary>
Task<Result<IReadOnlyList<NavigationDetail>, AeroError>> ListCultureVariantsAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
    /// ForkToCultureAsync method.
    /// </summary>
Task<Result<NavMenuDocument, AeroError>> ForkToCultureAsync(
        long id,
        string targetCulture,
        long? userId = null,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// GetDefaultIdAsync method.
    /// </summary>
Task<Result<long?, AeroError>> GetDefaultIdAsync(long siteId, CancellationToken cancellationToken = default);

        /// <summary>
    /// GetPublishedSnapshotAsync method.
    /// </summary>
Task<Result<NavMenuSnapshot?, AeroError>> GetPublishedSnapshotAsync(
        long id,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// ResolveSnapshotAsync method.
    /// </summary>
Task<Result<NavMenuSnapshot?, AeroError>> ResolveSnapshotAsync(
        long siteId,
        long? pageOverrideId = null,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// CreateAsync method.
    /// </summary>
Task<Result<NavMenuDocument, AeroError>> CreateAsync(
        CreateNavigationRequest request,
        long? userId = null,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// SaveDraftAsync method.
    /// </summary>
Task<Result<NavMenuDocument, AeroError>> SaveDraftAsync(
        long id,
        UpdateNavigationRequest request,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// PublishAsync method.
    /// </summary>
Task<Result<NavMenuDocument, AeroError>> PublishAsync(
        long id,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// SetDefaultAsync method.
    /// </summary>
Task<Result<bool, AeroError>> SetDefaultAsync(
        long id,
        long? userId = null,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// ArchiveAsync method.
    /// </summary>
Task<Result<bool, AeroError>> ArchiveAsync(
        long id,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default);
}
