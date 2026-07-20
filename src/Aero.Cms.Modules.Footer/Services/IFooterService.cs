using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Modules.Footer.Domain;

namespace Aero.Cms.Modules.Footer.Services;

/// <summary>
/// Provides site-scoped footer authoring and published-snapshot resolution.
/// </summary>
/// <remarks>
/// Authoring operations use the current site context. Methods that accept an explicit site
/// identifier resolve data for that site and require the caller to enforce any tenant boundary
/// appropriate to the host. The service does not cache results.
/// </remarks>
public interface IFooterService
{
    /// <summary>
    /// Lists non-archived footers owned by the current site.
    /// </summary>
    /// <param name="skip">The number of name-ordered matches to skip.</param>
    /// <param name="take">The maximum number of matches to return.</param>
    /// <param name="search">An optional case-insensitive substring filter over name and key.</param>
    /// <param name="cancellationToken">A token forwarded to the database query.</param>
    /// <returns>
    /// The page and total filtered count, or a database error. Pagination values are passed through
    /// without service-level range validation.
    /// </returns>
    Task<Result<(IReadOnlyList<FooterDocument> Items, long TotalCount), AeroError>> ListAsync(
        int skip = 0,
        int take = 20,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a footer only when it belongs to the current site.
    /// </summary>
    /// <param name="id">The footer identifier.</param>
    /// <param name="cancellationToken">A token forwarded to the document load.</param>
    /// <returns>
    /// The footer, or a not-found error for both missing and out-of-site identifiers; database
    /// failures, including cancellation in the current implementation, are returned as errors.
    /// </returns>
    Task<Result<FooterDocument, AeroError>> GetAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current-site metadata together with its latest editor snapshot and stream version.
    /// </summary>
    /// <param name="id">The footer identifier.</param>
    /// <param name="cancellationToken">A token forwarded to document and event-stream reads.</param>
    /// <returns>The combined detail, or the site-scoped lookup failure.</returns>
    /// <remarks>Failures while reading the snapshot or stream version after metadata lookup can propagate.</remarks>
    Task<Result<FooterDetail, AeroError>> GetDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists non-archived culture variants in the current footer's translation group.
    /// </summary>
    /// <param name="id">The current-site footer used to identify the translation group.</param>
    /// <param name="cancellationToken">A token forwarded to document and stream reads.</param>
    /// <returns>Culture-ordered details, or a lookup/database error.</returns>
    Task<Result<IReadOnlyList<FooterDetail>, AeroError>> ListCultureVariantsAsync(
        long id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an unpublished culture variant by cloning the source footer's editor snapshot.
    /// </summary>
    /// <param name="id">The current-site source footer identifier.</param>
    /// <param name="targetCulture">
    /// The requested target culture; invalid or blank input falls back to the site default culture.
    /// </param>
    /// <param name="userId">The optional actor recorded on the new stream.</param>
    /// <param name="cancellationToken">A token forwarded to reads and persistence.</param>
    /// <returns>
    /// The projected draft variant, or an error when the source cannot be read, a non-archived variant
    /// already exists, or persistence fails.
    /// </returns>
    /// <remarks>The operation does not publish the variant or emit a footer-changed message.</remarks>
    Task<Result<FooterDocument, AeroError>> ForkToCultureAsync(
        long id,
        string targetCulture,
        long? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the selected default footer identifier for an explicit site.
    /// </summary>
    /// <param name="siteId">The site settings document identifier to read.</param>
    /// <param name="cancellationToken">A token forwarded to the document load.</param>
    /// <returns>
    /// The selected identifier, or <see langword="null"/> when no selection exists. The current
    /// implementation also treats requested cancellation as a successful null result.
    /// </returns>
    /// <remarks>This method does not compare <paramref name="siteId"/> with the current site context.</remarks>
    Task<Result<long?, AeroError>> GetDefaultIdAsync(long siteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the latest published snapshot from an explicit footer stream.
    /// </summary>
    /// <param name="siteId">The site that must own the footer.</param>
    /// <param name="id">The footer identifier.</param>
    /// <param name="cancellationToken">A token forwarded to document and event-stream reads.</param>
    /// <returns>
    /// The latest published snapshot, or <see langword="null"/> when the footer is missing, archived,
    /// unpublished, or the current operation is cancelled.
    /// </returns>
    Task<Result<FooterSnapshot?, AeroError>> GetPublishedSnapshotAsync(
        long siteId,
        long id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the published snapshot for an explicit site and the current UI culture.
    /// </summary>
    /// <param name="siteId">The site whose footer should be resolved.</param>
    /// <param name="cancellationToken">A token forwarded to document, query, and stream reads.</param>
    /// <returns>
    /// A published snapshot, or <see langword="null"/> when none resolves or cancellation is requested.
    /// </returns>
    /// <remarks>
    /// A selected default is preferred. When it belongs to a translation group, an exact published
    /// culture variant is selected when available; there is no parent-culture fallback. Without a
    /// selected default, the oldest non-archived published footer is used. Results are not cached.
    /// </remarks>
    Task<Result<FooterSnapshot?, AeroError>> ResolveSnapshotAsync(
        long siteId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a current-site footer stream with an initial draft.
    /// </summary>
    /// <param name="request">The authoring metadata and snapshot content.</param>
    /// <param name="userId">The optional creating user identifier.</param>
    /// <param name="cancellationToken">A token forwarded to duplicate checks and persistence.</param>
    /// <returns>
    /// The projected draft document, or an error when the current site is unavailable, the
    /// site/culture/key already exists, snapshot validation fails, or persistence fails.
    /// </returns>
    Task<Result<FooterDocument, AeroError>> CreateAsync(
        CreateFooterRequest request,
        long? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the editor draft of a current-site footer using optimistic stream concurrency.
    /// </summary>
    /// <param name="id">The footer identifier.</param>
    /// <param name="request">The updated authoring metadata and snapshot.</param>
    /// <param name="expectedVersion">The event-stream version expected by the caller.</param>
    /// <param name="userId">The optional saving user identifier.</param>
    /// <param name="cancellationToken">A token forwarded to reads and persistence.</param>
    /// <returns>The projected document, a conflict/lookup error, or a persistence error.</returns>
    /// <remarks>The operation validates the mapped snapshot but does not publish it or emit a change message.</remarks>
    Task<Result<FooterDocument, AeroError>> SaveDraftAsync(
        long id,
        UpdateFooterRequest request,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the latest draft of a current-site footer using optimistic stream concurrency.
    /// </summary>
    /// <param name="id">The footer identifier.</param>
    /// <param name="expectedVersion">The event-stream version expected by the caller.</param>
    /// <param name="userId">The optional publishing user identifier.</param>
    /// <param name="cancellationToken">A token forwarded to reads, persistence, and messaging.</param>
    /// <returns>The projected published document, or a lookup, conflict, validation, database, or messaging error.</returns>
    /// <remarks>
    /// After the database commit, the implementation publishes a footer-changed message when a bus
    /// is configured. Messaging failure can therefore produce a failure result after publication
    /// has committed. A configured consumer may invalidate caches, but this service does not itself cache.
    /// </remarks>
    Task<Result<FooterDocument, AeroError>> PublishAsync(
        long id,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Selects a published, non-archived current-site footer as the site's default.
    /// </summary>
    /// <param name="id">The footer identifier.</param>
    /// <param name="userId">The optional user making the selection.</param>
    /// <param name="cancellationToken">A token forwarded to reads, persistence, and messaging.</param>
    /// <returns><see langword="true"/> when committed, or a lookup, validation, database, or messaging error.</returns>
    /// <remarks>
    /// The settings stream is not guarded by an expected version. A configured change message is
    /// published after commit, so message failure can be reported after the selection has committed.
    /// </remarks>
    Task<Result<bool, AeroError>> SetDefaultAsync(
        long id,
        long? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives a current-site footer using optimistic stream concurrency.
    /// </summary>
    /// <param name="id">The footer identifier.</param>
    /// <param name="expectedVersion">The event-stream version expected by the caller.</param>
    /// <param name="userId">The optional archiving user identifier.</param>
    /// <param name="cancellationToken">A token forwarded to reads, persistence, and messaging.</param>
    /// <returns><see langword="true"/> when committed, or a lookup, conflict, database, or messaging error.</returns>
    /// <remarks>
    /// Archiving does not clear an existing site-default selection. A configured change message is
    /// published after commit, so message failure can be reported after the archive has committed.
    /// </remarks>
    Task<Result<bool, AeroError>> ArchiveAsync(
        long id,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default);
}
