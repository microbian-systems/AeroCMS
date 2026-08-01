using Aero.Cms.Modules.Commerce.A2A.Models;

namespace Aero.Cms.Modules.Commerce.A2A.Services;

/// <summary>Persists A2A availability documents within an explicit tenant and site boundary.</summary>
public interface IA2ASettingsRepository
{
    /// <summary>Loads the setting document for one tenant and site, if it exists.</summary>
    Task<Result<A2ASettingsDocument?, AeroError>> GetAsync(long tenantId, long siteId, CancellationToken ct = default);

    /// <summary>Persists a scoped setting document.</summary>
    Task<Result<A2ASettingsDocument, AeroError>> SaveAsync(A2ASettingsDocument settings, CancellationToken ct = default);
}
