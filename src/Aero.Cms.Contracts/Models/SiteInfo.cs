namespace Aero.Cms.Contracts.Models;

/// <summary>
/// WASM-safe site information DTO.
/// Replaces SiteViewModel (which has Orleans [GenerateSerializer] attributes)
/// for use in WASM-only contexts. Map at the module boundary.
/// </summary>
public record SiteInfo(
    long Id,
    string? Name,
    string? PrimaryHost,
    bool IsEnabled,
    string? DefaultCulture,
    long TenantId);
