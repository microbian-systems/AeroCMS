using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Modules.Navigation.Domain;

namespace Aero.Cms.Modules.Navigation.Services;

/// <summary>
/// Manages site-scoped, event-sourced navigation menus and their published snapshots.
/// </summary>
/// <remarks>
/// Implementations use <see cref="ISiteContext"/> for manager-facing menu operations.
/// Most caught persistence failures are returned through <see cref="Result{T, TError}"/>.
/// Direct event-stream reads in <see cref="GetDetailAsync"/> and non-cancellation failures
/// during culture-variant resolution in <see cref="ResolveSnapshotAsync"/> can escape.
/// </remarks>
public interface INavMenuService
{
    /// <summary>
    /// Lists active navigation menus for the current manager site.
    /// </summary>
    /// <param name="skip">The number of ordered records to skip.</param>
    /// <param name="take">The maximum number of records to return.</param>
    /// <param name="search">An optional case-insensitive name or key fragment.</param>
    /// <param name="cancellationToken">The token used for the database query.</param>
    /// <returns>The matching page and total pre-pagination count, or a database failure.</returns>
Task<Result<(IReadOnlyList<NavMenuDocument> Items, long TotalCount), AeroError>> ListAsync(
        int skip = 0,
        int take = 20,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a navigation menu only when it belongs to the current manager site.
    /// </summary>
    /// <param name="id">The navigation document identifier.</param>
    /// <param name="cancellationToken">The token used for the database lookup.</param>
    /// <returns>The menu, a not-found/access-denied failure, or a database failure.</returns>
Task<Result<NavMenuDocument, AeroError>> GetAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a current-site menu with the snapshot appropriate for editing and its event-stream version.
    /// </summary>
    /// <param name="id">The navigation document identifier.</param>
    /// <param name="cancellationToken">The token used for document and event-stream reads.</param>
    /// <returns>The editor detail, or the failure returned while loading the menu.</returns>
    /// <remarks>
    /// The event-stream read occurs outside the implementation's exception handler, so persistence
    /// and cancellation exceptions from that read can propagate to the caller.
    /// </remarks>
Task<Result<NavigationDetail, AeroError>> GetDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the non-archived culture variants in the selected menu's translation group.
    /// </summary>
    /// <param name="id">The identifier of a current-site menu in the translation group.</param>
    /// <param name="cancellationToken">The token used for document and event-stream reads.</param>
    /// <returns>Culture-ordered editor details, or a not-found/access-denied or database failure.</returns>
Task<Result<IReadOnlyList<NavigationDetail>, AeroError>> ListCultureVariantsAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a draft culture variant by deep-copying a current-site menu's editor snapshot.
    /// </summary>
    /// <param name="id">The source navigation menu identifier.</param>
    /// <param name="targetCulture">The requested target culture; invalid values fall back to the platform default culture name.</param>
    /// <param name="userId">The optional actor recorded on the new stream events.</param>
    /// <param name="cancellationToken">The token used through the event-stream commit.</param>
    /// <returns>The new draft document, or a not-found, duplicate-culture, or database failure.</returns>
Task<Result<NavMenuDocument, AeroError>> ForkToCultureAsync(
        long id,
        string targetCulture,
        long? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the configured default navigation menu identifier for a site.
    /// </summary>
    /// <param name="siteId">The site whose settings document is queried.</param>
    /// <param name="cancellationToken">The token used for the database query.</param>
    /// <returns>The configured identifier, <see langword="null"/> when absent or cancelled, or a database failure.</returns>
    /// <remarks>
    /// The service does not authorize an arbitrary <paramref name="siteId"/> against the current
    /// manager site. Callers accepting a site identifier from outside a trusted rendering pipeline
    /// must enforce that boundary.
    /// </remarks>
Task<Result<long?, AeroError>> GetDefaultIdAsync(long siteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the latest published snapshot for a non-archived menu.
    /// </summary>
    /// <param name="siteId">The site that must own the navigation menu.</param>
    /// <param name="id">The navigation menu identifier.</param>
    /// <param name="cancellationToken">The token used for document and event-stream reads.</param>
    /// <returns>The latest published snapshot, <see langword="null"/> when unavailable or cancelled, or a database failure.</returns>
Task<Result<NavMenuSnapshot?, AeroError>> GetPublishedSnapshotAsync(
        long siteId,
        long id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a culture-aware published snapshot from a page override or the site's default menu.
    /// </summary>
    /// <param name="siteId">The site used for default-menu and culture-variant queries.</param>
    /// <param name="pageOverrideId">An optional navigation menu identifier supplied by page configuration.</param>
    /// <param name="cancellationToken">The token used for all persistence reads.</param>
    /// <returns>The published snapshot, <see langword="null"/> when none is available or resolution is cancelled, or a database failure.</returns>
    /// <remarks>
    /// The current UI culture selects a same-site published variant when available. Invalid or
    /// foreign overrides and defaults resolve to no snapshot. Requested cancellation during
    /// resolution returns no snapshot.
    /// </remarks>
Task<Result<NavMenuSnapshot?, AeroError>> ResolveSnapshotAsync(
        long siteId,
        long? pageOverrideId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a navigation event stream with creation and initial-draft events for the current site.
    /// </summary>
    /// <param name="request">The initial menu name, links, and optional logo.</param>
    /// <param name="userId">The optional actor recorded in audit fields.</param>
    /// <param name="cancellationToken">The token used through the event-stream commit.</param>
    /// <returns>The projected draft document, or an invalid-site, duplicate-key, or database failure.</returns>
Task<Result<NavMenuDocument, AeroError>> CreateAsync(
        CreateNavigationRequest request,
        long? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a validated draft snapshot to a current-site navigation stream.
    /// </summary>
    /// <param name="id">The navigation menu identifier.</param>
    /// <param name="request">The editor state to map to a snapshot.</param>
    /// <param name="expectedVersion">The expected event-stream version; values at or below zero disable the explicit pre-check.</param>
    /// <param name="userId">The optional actor recorded on the draft event.</param>
    /// <param name="cancellationToken">The token used through the optimistic append and commit.</param>
    /// <returns>The updated document, or a not-found, snapshot-rule/concurrency conflict, or database failure.</returns>
Task<Result<NavMenuDocument, AeroError>> SaveDraftAsync(
        long id,
        UpdateNavigationRequest request,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the latest draft snapshot using optimistic event-stream concurrency.
    /// </summary>
    /// <param name="id">The navigation menu identifier.</param>
    /// <param name="expectedVersion">The expected event-stream version; values at or below zero disable the explicit pre-check.</param>
    /// <param name="userId">The optional actor recorded on the publication event.</param>
    /// <param name="cancellationToken">The token used through persistence and change notification.</param>
    /// <returns>The published document, or a not-found, missing-draft, snapshot-rule/concurrency conflict, or database failure.</returns>
    /// <remarks>
    /// The stream commit precedes publication of the navigation-changed message. A message-bus
    /// failure can therefore be returned as a database failure after publication was persisted.
    /// </remarks>
Task<Result<NavMenuDocument, AeroError>> PublishAsync(
        long id,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a published current-site navigation menu as the site's default.
    /// </summary>
    /// <param name="id">The navigation menu identifier.</param>
    /// <param name="userId">The optional actor recorded on the settings event.</param>
    /// <param name="cancellationToken">The token used through persistence and change notification.</param>
    /// <returns><see langword="true"/> on success, or a not-found, state, or database failure.</returns>
    /// <remarks>
    /// The settings event is committed before the change notification is published.
    /// </remarks>
Task<Result<bool, AeroError>> SetDefaultAsync(
        long id,
        long? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends an archive event to a current-site navigation stream.
    /// </summary>
    /// <param name="id">The navigation menu identifier.</param>
    /// <param name="expectedVersion">The expected event-stream version; values at or below zero disable the explicit pre-check.</param>
    /// <param name="userId">The optional actor recorded on the archive event.</param>
    /// <param name="cancellationToken">The token used through persistence and change notification.</param>
    /// <returns><see langword="true"/> on success, or a not-found, concurrency, or database failure.</returns>
    /// <remarks>
    /// Archiving does not clear a site's default-menu setting. Persistence precedes the
    /// navigation-changed notification.
    /// </remarks>
Task<Result<bool, AeroError>> ArchiveAsync(
        long id,
        long expectedVersion,
        long? userId = null,
        CancellationToken cancellationToken = default);
}
