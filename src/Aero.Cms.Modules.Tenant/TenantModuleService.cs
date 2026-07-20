using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Extensions;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Tenant;

// todo - we'll need to add a get tenant sites from tenant service, and a get tenant from site service, to support multi-tenancy features in the future.
// For now, we'll just have a simple tenant management service that can be used to create and manage tenants.

/// <summary>
/// Defines application-level tenant lifecycle operations.
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Validates and persists a tenant.
    /// </summary>
    /// <param name="tenant">The tenant to validate and store.</param>
    /// <param name="ct">The token used for persistence.</param>
    /// <returns>The stored tenant, or a validation/persistence error.</returns>
Task<Result<TenantModel, AeroError>> CreateTenantAsync(TenantModel tenant, CancellationToken ct = default);
    /// <summary>
    /// Deletes a tenant by identifier.
    /// </summary>
    /// <param name="id">The tenant identifier.</param>
    /// <param name="ct">The token used for persistence.</param>
    /// <returns>A task that completes after the repository operation.</returns>
Task DeleteTenantAsync(long id, CancellationToken ct = default);
    /// <summary>
    /// Lists a page of tenants.
    /// </summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="num">The requested page size.</param>
    /// <param name="ct">The token used for the query.</param>
    /// <returns>The returned tenant page.</returns>
Task<IEnumerable<TenantModel>> GetAllTenantsAsync(int page = 1, int num = 10, CancellationToken ct = default);
    /// <summary>
    /// Finds a tenant by identifier.
    /// </summary>
    /// <param name="id">The tenant identifier.</param>
    /// <param name="ct">The token used for the lookup.</param>
    /// <returns>A populated option when found; otherwise an empty option.</returns>
Task<Option<TenantModel>> GetTenantByIdAsync(long id, CancellationToken ct = default);
    /// <summary>
    /// Stores a replacement tenant document without applying create-time validation.
    /// </summary>
    /// <param name="tenant">The replacement tenant document.</param>
    /// <param name="ct">The token used for persistence.</param>
    /// <returns>The same tenant after commit.</returns>
Task<TenantModel> UpdateTenantAsync(TenantModel tenant, CancellationToken ct = default);
}

/// <summary>
/// Coordinates tenant validation, repository persistence, and service-level logging.
/// </summary>
/// <param name="repo">The tenant repository.</param>
/// <param name="log">The service logger.</param>
public class TenantService(ITenantRepository repo, ILogger<TenantService> log) : ITenantService
{

    /// <inheritdoc />
public async Task<IEnumerable<TenantModel>> GetAllTenantsAsync(int page = 1, int num = 10, CancellationToken ct = default)
    {
        var res = await repo.GetAllAsync(page, num, ct);
        return res;
    }
    /// <inheritdoc />
public async Task<Option<TenantModel>> GetTenantByIdAsync(long id, CancellationToken ct = default)
    {
        var tenant = await repo.FindByIdAsync(id, ct);

        return tenant;

    }

    /// <inheritdoc />
    /// <remarks>
    /// Validation and persistence exceptions, including cancellation raised inside the
    /// <c>try</c> block, are logged and converted to <see cref="AeroError.Error"/>.
    /// </remarks>
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

    /// <inheritdoc />
public async Task<TenantModel> UpdateTenantAsync(TenantModel tenant, CancellationToken ct = default)
    {
        return await repo.UpdateAsync(tenant, ct);
    }

    /// <inheritdoc />
    /// <remarks>The repository's Boolean deletion result is discarded.</remarks>
public async Task DeleteTenantAsync(long id, CancellationToken ct = default)
    {
        await repo.DeleteAsync(id, ct);
    }
}
