using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Requests;

namespace Aero.Cms.Abstractions.Services;

public interface IAeroAliasService
{
    /// <summary>Create alias. Throws <see cref="InvalidOperationException"/> on grain error.</summary>
    Task<AliasViewModel> CreateAsync(CreateAliasRequest request, CancellationToken ct = default);

    /// <summary>Delete alias. Throws <see cref="InvalidOperationException"/> on grain error.</summary>
    Task DeleteAsync(DeleteAliasRequest request, CancellationToken ct = default);

    Task<List<AliasViewModel>> GetAllAliasesAsync(long? siteId = null, CancellationToken ct = default);
    Task<AeroRequestResponse<AliasViewModel>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<AeroRequestResponse<AliasViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct = default);
    Task<AeroRequestResponse<AliasViewModel>> GetBySiteIdAsync(long siteId, AeroSearchFilter? filter, int page = 1, int rows = 10, CancellationToken ct = default);
    Task<AeroRequestResponse<AliasViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct = default);
    Task<AliasViewModel> GetStateAsync(CancellationToken ct);
    Task<AeroRequestResponse<AliasViewModel>> UpdateAsync(UpdateAliasRequest request, CancellationToken ct = default);
    Task UpdateStateAsync(AliasViewModel state, CancellationToken ct);
}

public class AeroAliasService(IGrainFactory grainFactory) : IAeroAliasService
{
    IAeroAliasActor actor => grainFactory.GetGrain<IAeroAliasActor>(0, "aero");

    public async Task<AliasViewModel> CreateAsync(CreateAliasRequest request, CancellationToken ct = default)
    {
        var result = await actor.CreateAsync(request, ct);
        return Unwrap(result);
    }

    public async Task DeleteAsync(DeleteAliasRequest request, CancellationToken ct = default)
    {
        var result = await actor.DeleteAsync(request, ct);
        Unwrap(result); // throws on error
    }

    public Task<List<AliasViewModel>> GetAllAliasesAsync(long? siteId = null, CancellationToken ct = default)
    {
        var result = actor.GetAllAliasesAsync(siteId, ct);
        return result;
    }

    public Task<AeroRequestResponse<AliasViewModel>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var result = actor.GetByIdAsync(id, ct);
        return result;
    }

    public async Task<AeroRequestResponse<AliasViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct = default)
    {
        var result = await actor.GetByIdsAsync(ids, ct);
        return result;
    }

    public async Task<AeroRequestResponse<AliasViewModel>> GetBySiteIdAsync(long siteId, AeroSearchFilter? filter, int page = 1, int rows = 10, CancellationToken ct = default)
    {
        var result = await actor.GetBySiteIdAsync(siteId, page, rows, ct);
        return result;
    }

    public async Task<AeroRequestResponse<AliasViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct = default)
    {
        var result = await actor.GetBySlugAsync(siteId, slug, ct);
        return result;
    }

    public Task<AliasViewModel> GetStateAsync(CancellationToken ct)
    {
        var result = actor.GetStateAsync(ct);
        return result;
    }

    public Task<AeroRequestResponse<AliasViewModel>> UpdateAsync(UpdateAliasRequest request, CancellationToken ct = default)
    {
        var result = actor.UpdateAsync(request, ct);
        return result;
    }

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
