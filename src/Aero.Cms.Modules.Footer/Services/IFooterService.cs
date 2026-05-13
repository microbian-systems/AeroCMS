using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Modules.Footer.Domain;

namespace Aero.Cms.Modules.Footer.Services;

public interface IFooterService
{
    Task<Result<(IReadOnlyList<FooterDocument> Items, long TotalCount), AeroError>> ListAsync(
        int skip = 0,
        int take = 20,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<Result<FooterDocument, AeroError>> GetAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<FooterDetail, AeroError>> GetDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<long?, AeroError>> GetDefaultIdAsync(long siteId, CancellationToken cancellationToken = default);
    Task<Result<FooterSnapshot?, AeroError>> GetPublishedSnapshotAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<FooterSnapshot?, AeroError>> ResolveSnapshotAsync(long siteId, CancellationToken cancellationToken = default);

    Task<Result<FooterDocument, AeroError>> CreateAsync(
        CreateFooterRequest request,
        long? userId = null,
        CancellationToken cancellationToken = default);

    Task<Result<FooterDocument, AeroError>> SaveDraftAsync(
        long id,
        UpdateFooterRequest request,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default);

    Task<Result<FooterDocument, AeroError>> PublishAsync(
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
