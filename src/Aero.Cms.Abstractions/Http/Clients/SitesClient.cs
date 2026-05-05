using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Abstractions.Http.Clients;

public interface ISitesHttpClient
{
    Task<Result<IReadOnlyList<SiteViewModel>, AeroError>> GetAllAsync(CancellationToken ct = default);
    Task<Result<SiteViewModel, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Result<SiteViewModel, AeroError>> CreateAsync(CreateSiteRequest request, CancellationToken ct = default);
    Task<Result<SiteViewModel, AeroError>> UpdateAsync(long id, UpdateSiteRequest request, CancellationToken ct = default);
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
}

public class SitesHttpClient(HttpClient httpClient, ILogger<SitesHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), ISitesHttpClient
{
    public override string Path => "admin/sites";

    public Task<Result<IReadOnlyList<SiteViewModel>, AeroError>> GetAllAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<SiteViewModel>>("", ct);

    public Task<Result<SiteViewModel, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
        => GetAsync<SiteViewModel>(id.ToString(), ct);

    public Task<Result<SiteViewModel, AeroError>> CreateAsync(CreateSiteRequest request, CancellationToken ct = default)
        => PostAsync<CreateSiteRequest, SiteViewModel>(string.Empty, request, ct);

    public Task<Result<SiteViewModel, AeroError>> UpdateAsync(long id, UpdateSiteRequest request, CancellationToken ct = default)
        => PutAsync<UpdateSiteRequest, SiteViewModel>(id.ToString(), request, ct);

    public Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
        => MapBoolResult(base.DeleteAsync(id.ToString(), ct));

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
