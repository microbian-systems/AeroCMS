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
    /// GetAllAsync method.
    /// </summary>
Task<Result<IReadOnlyList<SiteInfo>, AeroError>> GetAllAsync(CancellationToken ct = default);
        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
Task<Result<SiteInfo, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// GetDefaultAsync method.
    /// </summary>
Task<Result<SiteInfo, AeroError>> GetDefaultAsync(CancellationToken ct = default);
}
