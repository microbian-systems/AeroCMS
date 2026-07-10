using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Extensions;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Tenant;

// todo - we'll need to add a get tenant sites from tenant service, and a get tenant from site service, to support multi-tenancy features in the future.
// For now, we'll just have a simple tenant management service that can be used to create and manage tenants.

/// <summary>
/// Defines an interface for ITenantService.
/// </summary>
public interface ITenantService
{
        /// <summary>
    /// CreateTenantAsync method.
    /// </summary>
Task<Result<TenantModel, AeroError>> CreateTenantAsync(TenantModel tenant, CancellationToken ct = default);
        /// <summary>
    /// DeleteTenantAsync method.
    /// </summary>
Task DeleteTenantAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// GetAllTenantsAsync method.
    /// </summary>
Task<IEnumerable<TenantModel>> GetAllTenantsAsync(int page = 1, int num = 10, CancellationToken ct = default);
        /// <summary>
    /// GetTenantByIdAsync method.
    /// </summary>
Task<Option<TenantModel>> GetTenantByIdAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// UpdateTenantAsync method.
    /// </summary>
Task<TenantModel> UpdateTenantAsync(TenantModel tenant, CancellationToken ct = default);
}

/// <summary>
/// Represents a class for TenantService.
/// </summary>
public class TenantService(ITenantRepository repo, ILogger<TenantService> log) : ITenantService
{

        /// <summary>
    /// GetAllTenantsAsync method.
    /// </summary>
public async Task<IEnumerable<TenantModel>> GetAllTenantsAsync(int page = 1, int num = 10, CancellationToken ct = default)
    {
        var res = await repo.GetAllAsync(page, num, ct);
        return res;
    }
        /// <summary>
    /// GetTenantByIdAsync method.
    /// </summary>
public async Task<Option<TenantModel>> GetTenantByIdAsync(long id, CancellationToken ct = default)
    {
        var tenant = await repo.FindByIdAsync(id, ct);

        return tenant;

    }

        /// <summary>
    /// CreateTenantAsync method.
    /// </summary>
public async Task<Result<TenantModel, AeroError>> CreateTenantAsync(TenantModel tenant, CancellationToken ct = default)
    {
        var validator = new TenantValidator();
        var result = validator.Validate(tenant);

        if (!result.IsValid)
        {
            var error = AeroError.CreateError(result.Errors.ConcatenateLines(e => e.ErrorMessage));
            log.LogWarning("Tenant validation failed: {ValidationErrors}", error.msg);
            return error;
        }

        try
        {
            var created = await repo.InsertAsync(tenant, ct);
            log.LogInformation("Created tenant {TenantId} with hostname {Hostname}", created.Id, created.Hostname);
            return created;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to create tenant {TenantName}", tenant.Name);
            return AeroError.CreateError($"Failed to create tenant: {ex.Message}");
        }
    }

        /// <summary>
    /// UpdateTenantAsync method.
    /// </summary>
public async Task<TenantModel> UpdateTenantAsync(TenantModel tenant, CancellationToken ct = default)
    {
        return await repo.UpdateAsync(tenant, ct);
    }

        /// <summary>
    /// DeleteTenantAsync method.
    /// </summary>
public async Task DeleteTenantAsync(long id, CancellationToken ct = default)
    {
        await repo.DeleteAsync(id, ct);
    }
}
