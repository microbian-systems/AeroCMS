using Aero.Cms.Modules.Commerce.A2A.Models;

namespace Aero.Cms.Modules.Commerce.A2A.Services;

/// <summary>Resolves and changes the A2A availability switch within an explicit site scope.</summary>
public interface IA2ASettingsService
{
    /// <summary>Gets the site's A2A setting, returning disabled when no setting has been persisted.</summary>
    Task<Result<A2ASettingsResponse, AeroError>> GetAsync(long tenantId, long siteId, CancellationToken ct = default);

    /// <summary>Validates and applies the site's A2A availability setting.</summary>
    Task<Result<A2ASettingsResponse, AeroError>> UpdateAsync(long tenantId, long siteId, UpdateA2ASettingsRequest request, string? actorId, CancellationToken ct = default);
}
