using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Modules.Footer.Domain;

namespace Aero.Cms.Modules.Footer.Services;

/// <summary>
/// Defines an interface for IFooterService.
/// </summary>
public interface IFooterService
{
        /// <summary>
    /// ListAsync method.
    /// </summary>
Task<Result<(IReadOnlyList<FooterDocument> Items, long TotalCount), AeroError>> ListAsync(
        int skip = 0,
        int take = 20,
        string? search = null,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// GetAsync method.
    /// </summary>
Task<Result<FooterDocument, AeroError>> GetAsync(long id, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetDetailAsync method.
    /// </summary>
Task<Result<FooterDetail, AeroError>> GetDetailAsync(long id, CancellationToken cancellationToken = default);
        /// <summary>
    /// ListCultureVariantsAsync method.
    /// </summary>
Task<Result<IReadOnlyList<FooterDetail>, AeroError>> ListCultureVariantsAsync(long id, CancellationToken cancellationToken = default);
        /// <summary>
    /// ForkToCultureAsync method.
    /// </summary>
Task<Result<FooterDocument, AeroError>> ForkToCultureAsync(
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
Task<Result<FooterSnapshot?, AeroError>> GetPublishedSnapshotAsync(long id, CancellationToken cancellationToken = default);
        /// <summary>
    /// ResolveSnapshotAsync method.
    /// </summary>
Task<Result<FooterSnapshot?, AeroError>> ResolveSnapshotAsync(long siteId, CancellationToken cancellationToken = default);

        /// <summary>
    /// CreateAsync method.
    /// </summary>
Task<Result<FooterDocument, AeroError>> CreateAsync(
        CreateFooterRequest request,
        long? userId = null,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// SaveDraftAsync method.
    /// </summary>
Task<Result<FooterDocument, AeroError>> SaveDraftAsync(
        long id,
        UpdateFooterRequest request,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// PublishAsync method.
    /// </summary>
Task<Result<FooterDocument, AeroError>> PublishAsync(
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
