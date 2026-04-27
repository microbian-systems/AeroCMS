using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Requests;

namespace Aero.Cms.Abstractions.Services;


public class AeroAliasService(IGrainFactory grainFactory)
{
    IAeroAliasActor actor => grainFactory.GetGrain<IAeroAliasActor>(0, "aero");

    public async Task<AeroRequestResponse<AliasViewModel>> CreateAsync(CreateAliasRequest request, CancellationToken ct = default)
    {
        var result = await actor.CreateAsync(request, ct);

        return result;
    }

    public async Task<AeroRequestResponse<AliasViewModel>> DeleteAsync(DeleteAliasRequest request, CancellationToken ct = default)
    {
        var result = await actor.DeleteAsync(request, ct);
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
}
