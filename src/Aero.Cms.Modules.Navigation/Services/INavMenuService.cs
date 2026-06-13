using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Modules.Navigation.Domain;

namespace Aero.Cms.Modules.Navigation.Services;

public interface INavMenuService
{
    Task<Result<(IReadOnlyList<NavMenuDocument> Items, long TotalCount), AeroError>> ListAsync(
        int skip = 0,
        int take = 20,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<Result<NavMenuDocument, AeroError>> GetAsync(long id, CancellationToken cancellationToken = default);

    Task<Result<NavigationDetail, AeroError>> GetDetailAsync(long id, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<NavigationDetail>, AeroError>> ListCultureVariantsAsync(long id, CancellationToken cancellationToken = default);

    Task<Result<NavMenuDocument, AeroError>> ForkToCultureAsync(
        long id,
        string targetCulture,
        long? userId = null,
        CancellationToken cancellationToken = default);

    Task<Result<long?, AeroError>> GetDefaultIdAsync(long siteId, CancellationToken cancellationToken = default);

    Task<Result<NavMenuSnapshot?, AeroError>> GetPublishedSnapshotAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<Result<NavMenuSnapshot?, AeroError>> ResolveSnapshotAsync(
        long siteId,
        long? pageOverrideId = null,
        CancellationToken cancellationToken = default);

    Task<Result<NavMenuDocument, AeroError>> CreateAsync(
        CreateNavigationRequest request,
        long? userId = null,
        CancellationToken cancellationToken = default);

    Task<Result<NavMenuDocument, AeroError>> SaveDraftAsync(
        long id,
        UpdateNavigationRequest request,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default);

    Task<Result<NavMenuDocument, AeroError>> PublishAsync(
        long id,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default);

    Task<Result<bool, AeroError>> SetDefaultAsync(
        long id,
        long? userId = null,
        CancellationToken cancellationToken = default);

    Task<Result<bool, AeroError>> ArchiveAsync(
        long id,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default);
}
