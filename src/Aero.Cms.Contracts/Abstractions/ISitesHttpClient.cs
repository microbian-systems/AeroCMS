using Aero.Cms.Contracts.Models;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Contracts.Abstractions;

/// <summary>
/// HTTP client for reading site data. WASM-safe — no Orleans dependencies.
/// Read-only operations; CRUD operations that use CreateSiteRequest/UpdateSiteRequest
/// live in the full ISitesHttpClient in Aero.Cms.Abstractions.
/// </summary>
public interface ISitesHttpClient
{
    /// <summary>
    /// Asynchronously retrieves the sites available to the caller.
    /// </summary>
    /// <param name="ct">A token that can cancel the in-progress request.</param>
    /// <returns>
    /// A successful result containing a read-only site collection; otherwise, an
    /// <see cref="AeroError"/> describing why the request could not be completed.
    /// </returns>
    Task<Result<IReadOnlyList<SiteInfo>, AeroError>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Asynchronously retrieves a site by its identifier.
    /// </summary>
    /// <param name="id">The site identifier to retrieve.</param>
    /// <param name="ct">A token that can cancel the in-progress request.</param>
    /// <returns>
    /// A successful result containing the requested site; otherwise, an
    /// <see cref="AeroError"/> describing why the request could not be completed.
    /// </returns>
    Task<Result<SiteInfo, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Asynchronously retrieves the default site selected by the server.
    /// </summary>
    /// <param name="ct">A token that can cancel the in-progress request.</param>
    /// <returns>
    /// A successful result containing the default site; otherwise, an
    /// <see cref="AeroError"/> describing why the request could not be completed.
    /// </returns>
    Task<Result<SiteInfo, AeroError>> GetDefaultAsync(CancellationToken ct = default);
}
