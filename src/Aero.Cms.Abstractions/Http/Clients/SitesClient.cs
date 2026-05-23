using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Abstractions.Http.Clients;

public interface ISitesHttpClient
{
    Task<Result<IReadOnlyList<SiteViewModel>, AeroError>> GetAllAsync(CancellationToken ct = default);
    Task<Result<SiteViewModel, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Result<SiteViewModel, AeroError>> GetDefaultAsync(CancellationToken ct = default);
    Task<Result<SiteViewModel, AeroError>> CreateAsync(CreateSiteRequest request, CancellationToken ct = default);
    Task<Result<SiteViewModel, AeroError>> UpdateAsync(long id, UpdateSiteRequest request, CancellationToken ct = default);
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
}

public class SitesHttpClient(HttpClient httpClient, ILogger<SitesHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), ISitesHttpClient, Aero.Cms.Contracts.Abstractions.ISitesHttpClient
{
    public override string Path => "admin/sites";

    public Task<Result<IReadOnlyList<SiteViewModel>, AeroError>> GetAllAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<SiteViewModel>>("", ct);

    public Task<Result<SiteViewModel, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
        => GetAsync<SiteViewModel>(id.ToString(), ct);

    public Task<Result<SiteViewModel, AeroError>> GetDefaultAsync(CancellationToken ct = default)
        => GetAsync<SiteViewModel>("default", ct);

    public Task<Result<SiteViewModel, AeroError>> CreateAsync(CreateSiteRequest request, CancellationToken ct = default)
        => PostAsync<CreateSiteRequest, SiteViewModel>(string.Empty, request, ct);

    public Task<Result<SiteViewModel, AeroError>> UpdateAsync(long id, UpdateSiteRequest request, CancellationToken ct = default)
        => PutAsync<UpdateSiteRequest, SiteViewModel>(id.ToString(), request, ct);

    public Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
        => MapBoolResult(base.DeleteAsync(id.ToString(), ct));

    // ── Contracts.ISitesHttpClient implementation (SiteInfo, no Orleans deps) ──

    async Task<Result<IReadOnlyList<SiteInfo>, AeroError>> Contracts.Abstractions.ISitesHttpClient.GetAllAsync(CancellationToken ct)
    {
        var result = await GetAllAsync(ct);
        return result switch
        {
            Result<IReadOnlyList<SiteViewModel>, AeroError>.Ok ok => new Result<IReadOnlyList<SiteInfo>, AeroError>.Ok(
                ok.Value.Select(MapToSiteInfo).ToList()),
            Result<IReadOnlyList<SiteViewModel>, AeroError>.Failure f => new Result<IReadOnlyList<SiteInfo>, AeroError>.Failure(f.Error),
            _ => new Result<IReadOnlyList<SiteInfo>, AeroError>.Failure(AeroError.CreateError("Unexpected result"))
        };
    }

    async Task<Result<SiteInfo, AeroError>> Contracts.Abstractions.ISitesHttpClient.GetByIdAsync(long id, CancellationToken ct)
    {
        var result = await GetByIdAsync(id, ct);
        return result switch
        {
            Result<SiteViewModel, AeroError>.Ok ok => new Result<SiteInfo, AeroError>.Ok(MapToSiteInfo(ok.Value)),
            Result<SiteViewModel, AeroError>.Failure f => new Result<SiteInfo, AeroError>.Failure(f.Error),
            _ => new Result<SiteInfo, AeroError>.Failure(AeroError.CreateError("Unexpected result"))
        };
    }

    async Task<Result<SiteInfo, AeroError>> Contracts.Abstractions.ISitesHttpClient.GetDefaultAsync(CancellationToken ct)
    {
        var result = await GetDefaultAsync(ct);
        return result switch
        {
            Result<SiteViewModel, AeroError>.Ok ok => new Result<SiteInfo, AeroError>.Ok(MapToSiteInfo(ok.Value)),
            Result<SiteViewModel, AeroError>.Failure f => new Result<SiteInfo, AeroError>.Failure(f.Error),
            _ => new Result<SiteInfo, AeroError>.Failure(AeroError.CreateError("Unexpected result"))
        };
    }

    private static SiteInfo MapToSiteInfo(SiteViewModel vm) => new(
        vm.Id, vm.Name, vm.PrimaryHost, vm.IsEnabled, vm.DefaultCulture, vm.TenantId);

    private static async Task<Result<bool, AeroError>> MapBoolResult(Task<Result<HttpResponseMessage, AeroError>> task)
    {
        var result = await task;
        return result switch
        {
            Result<HttpResponseMessage, AeroError>.Ok => new Result<bool, AeroError>.Ok(true),
            Result<HttpResponseMessage, AeroError>.Failure f => new Result<bool, AeroError>.Failure(f.Error),
            _ => new Result<bool, AeroError>.Failure(AeroError.CreateError("Unexpected result"))
        };
    }
}
