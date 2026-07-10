using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Requests;

namespace Aero.Cms.Abstractions.Services;

/// <summary>
/// Defines an interface for IAeroAliasService.
/// </summary>
public interface IAeroAliasService
{
    /// <summary>Create alias. Throws <see cref="InvalidOperationException"/> on grain error.</summary>
    Task<AliasViewModel> CreateAsync(CreateAliasRequest request, CancellationToken ct = default);

    /// <summary>Delete alias. Throws <see cref="InvalidOperationException"/> on grain error.</summary>
    Task DeleteAsync(DeleteAliasRequest request, CancellationToken ct = default);

        /// <summary>
    /// GetAllAliasesAsync method.
    /// </summary>
Task<List<AliasViewModel>> GetAllAliasesAsync(long? siteId = null, CancellationToken ct = default);
        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
Task<AeroRequestResponse<AliasViewModel>> GetByIdAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// GetByIdsAsync method.
    /// </summary>
Task<AeroRequestResponse<AliasViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct = default);
        /// <summary>
    /// GetBySiteIdAsync method.
    /// </summary>
Task<AeroRequestResponse<AliasViewModel>> GetBySiteIdAsync(long siteId, AeroSearchFilter? filter, int page = 1, int rows = 10, CancellationToken ct = default);
        /// <summary>
    /// GetBySlugAsync method.
    /// </summary>
Task<AeroRequestResponse<AliasViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct = default);
        /// <summary>
    /// GetStateAsync method.
    /// </summary>
Task<AliasViewModel> GetStateAsync(CancellationToken ct);
        /// <summary>
    /// UpdateAsync method.
    /// </summary>
Task<AeroRequestResponse<AliasViewModel>> UpdateAsync(UpdateAliasRequest request, CancellationToken ct = default);
        /// <summary>
    /// UpdateStateAsync method.
    /// </summary>
Task UpdateStateAsync(AliasViewModel state, CancellationToken ct);
}

/// <summary>
/// Represents a class for AeroAliasService.
/// </summary>
public class AeroAliasService(IGrainFactory grainFactory) : IAeroAliasService
{
    IAeroAliasActor actor => grainFactory.GetGrain<IAeroAliasActor>(0, "aero");

        /// <summary>
    /// CreateAsync method.
    /// </summary>
public async Task<AliasViewModel> CreateAsync(CreateAliasRequest request, CancellationToken ct = default)
    {
        var result = await actor.CreateAsync(request, ct);
        return Unwrap(result);
    }

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public async Task DeleteAsync(DeleteAliasRequest request, CancellationToken ct = default)
    {
        var result = await actor.DeleteAsync(request, ct);
        Unwrap(result); // throws on error
    }

        /// <summary>
    /// GetAllAliasesAsync method.
    /// </summary>
public Task<List<AliasViewModel>> GetAllAliasesAsync(long? siteId = null, CancellationToken ct = default)
    {
        var result = actor.GetAllAliasesAsync(siteId, ct);
        return result;
    }

        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
public Task<AeroRequestResponse<AliasViewModel>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var result = actor.GetByIdAsync(id, ct);
        return result;
    }

        /// <summary>
    /// GetByIdsAsync method.
    /// </summary>
public async Task<AeroRequestResponse<AliasViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct = default)
    {
        var result = await actor.GetByIdsAsync(ids, ct);
        return result;
    }

        /// <summary>
    /// GetBySiteIdAsync method.
    /// </summary>
public async Task<AeroRequestResponse<AliasViewModel>> GetBySiteIdAsync(long siteId, AeroSearchFilter? filter, int page = 1, int rows = 10, CancellationToken ct = default)
    {
        var result = await actor.GetBySiteIdAsync(siteId, page, rows, ct);
        return result;
    }

        /// <summary>
    /// GetBySlugAsync method.
    /// </summary>
public async Task<AeroRequestResponse<AliasViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct = default)
    {
        var result = await actor.GetBySlugAsync(siteId, slug, ct);
        return result;
    }

        /// <summary>
    /// GetStateAsync method.
    /// </summary>
public Task<AliasViewModel> GetStateAsync(CancellationToken ct)
    {
        var result = actor.GetStateAsync(ct);
        return result;
    }

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
public Task<AeroRequestResponse<AliasViewModel>> UpdateAsync(UpdateAliasRequest request, CancellationToken ct = default)
    {
        var result = actor.UpdateAsync(request, ct);
        return result;
    }

        /// <summary>
    /// UpdateStateAsync method.
    /// </summary>
public async Task UpdateStateAsync(AliasViewModel state, CancellationToken ct)
    {
        await actor.UpdateStateAsync(state, ct);
    }

    private static AliasViewModel Unwrap(AeroRequestResponse<AliasViewModel> result)
    {
        if (!string.IsNullOrEmpty(result.error?.Message))
            throw new InvalidOperationException(result.error.Message);

        return result.data;
    }
}
