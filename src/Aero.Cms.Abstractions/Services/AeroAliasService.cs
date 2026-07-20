using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Requests;

namespace Aero.Cms.Abstractions.Services;

/// <summary>
/// Defines an interface for IAeroAliasService.
/// </summary>
public interface IAeroAliasService
{
    /// <summary>Create alias. Throws <see cref="InvalidOperationException"/> on grain error.</summary>
    Task<AliasViewModel> CreateAsync(CreateAliasRequest request, long siteId, CancellationToken ct = default);

    /// <summary>Delete alias. Throws <see cref="InvalidOperationException"/> on grain error.</summary>
    Task DeleteAsync(DeleteAliasRequest request, long siteId, CancellationToken ct = default);

        /// <summary>
    /// GetAllAliasesAsync method.
    /// </summary>
Task<List<AliasViewModel>> GetAllAliasesAsync(long siteId, CancellationToken ct = default);
        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
Task<AeroRequestResponse<AliasViewModel>> GetByIdAsync(long id, long siteId, CancellationToken ct = default);
        /// <summary>
    /// GetBySiteIdAsync method.
    /// </summary>
Task<AeroRequestResponse<AliasViewModel>> GetBySiteIdAsync(long siteId, AeroSearchFilter? filter, int page = 1, int rows = 10, CancellationToken ct = default);
        /// <summary>
    /// GetBySlugAsync method.
    /// </summary>
Task<AeroRequestResponse<AliasViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct = default);
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
public async Task<AliasViewModel> CreateAsync(CreateAliasRequest request, long siteId, CancellationToken ct = default)
    {
        var result = await actor.CreateAliasAsync(request, siteId, ct);
        return Unwrap(result);
    }

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public async Task DeleteAsync(DeleteAliasRequest request, long siteId, CancellationToken ct = default)
    {
        var result = await actor.DeleteAliasAsync(request.Id, siteId, ct);
        Unwrap(result); // throws on error
    }

        /// <summary>
    /// GetAllAliasesAsync method.
    /// </summary>
public Task<List<AliasViewModel>> GetAllAliasesAsync(long siteId, CancellationToken ct = default)
    {
        var result = actor.GetAllAliasesAsync(siteId, ct);
        return result;
    }

        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
public Task<AeroRequestResponse<AliasViewModel>> GetByIdAsync(long id, long siteId, CancellationToken ct = default)
    {
        var result = actor.GetByIdAsync(id, siteId, ct);
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


    private static AliasViewModel Unwrap(AeroRequestResponse<AliasViewModel> result)
    {
        if (!string.IsNullOrEmpty(result.error?.Message))
            throw new InvalidOperationException(result.error.Message);

        return result.data;
    }
}
