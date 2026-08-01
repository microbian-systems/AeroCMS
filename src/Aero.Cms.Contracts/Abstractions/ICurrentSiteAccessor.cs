using Aero.Cms.Contracts.Models;

namespace Aero.Cms.Contracts.Abstractions;

/// <summary>
/// Reads and requests changes to the current site context for manager clients.
/// </summary>
/// <remarks>
/// The current HTTP-backed implementation communicates with endpoints that manage the
/// <c>AeroCms.SiteId</c> cookie and maintains a small in-memory identifier cache. Its methods
/// do not return an operation-success value, and suppressed HTTP or transport failures can be
/// observationally indistinguishable from no selected site.
/// </remarks>
public interface ICurrentSiteAccessor
{
    /// <summary>
    /// Raised when the accessor reports a local current-site change.
    /// </summary>
    /// <remarks>
    /// This notification is not proof that server-side state changed. The current implementation
    /// raises it after a successful set response and after any completed clear response, without
    /// checking whether the clear response was successful.
    /// </remarks>
    event Action? SiteChanged;

    /// <summary>
    /// Asynchronously retrieves information about the currently selected site.
    /// </summary>
    /// <returns>
    /// The selected site's information, or <see langword="null"/> when no site is selected or
    /// the current implementation cannot complete or deserialize the HTTP request.
    /// </returns>
    Task<SiteInfo?> GetCurrentSiteAsync();

    /// <summary>
    /// Asynchronously retrieves the identifier of the currently selected site.
    /// </summary>
    /// <returns>
    /// The identifier returned by the current-site lookup, falling back to the implementation's
    /// in-memory cache; otherwise <see langword="null"/>.
    /// </returns>
    Task<long?> GetCurrentSiteIdAsync();

    /// <summary>
    /// Requests that a site become current.
    /// </summary>
    /// <param name="siteId">The identifier of the site to select.</param>
    /// <remarks>
    /// The current implementation suppresses transport exceptions and unsuccessful HTTP
    /// responses. It updates its identifier cache and raises <see cref="SiteChanged"/> only
    /// after a successful HTTP response, so normal task completion does not guarantee selection.
    /// </remarks>
    Task SetCurrentSiteAsync(long siteId);

    /// <summary>
    /// Requests that the current-site selection be cleared.
    /// </summary>
    /// <remarks>
    /// The current implementation suppresses transport exceptions. If an HTTP response is
    /// received, it clears the local identifier cache and raises <see cref="SiteChanged"/>
    /// without checking the response status. Normal task completion does not guarantee that
    /// server-side state was cleared.
    /// </remarks>
    Task ClearCurrentSiteAsync();
}
