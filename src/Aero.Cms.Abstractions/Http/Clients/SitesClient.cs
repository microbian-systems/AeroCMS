using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Abstractions.Http.Clients;

/// <summary>
/// Defines an interface for ISitesHttpClient.
/// </summary>
public interface ISitesHttpClient
{
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
Task<Result<IReadOnlyList<SiteViewModel>, AeroError>> GetAllAsync(CancellationToken ct = default);
        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
Task<Result<SiteViewModel, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// GetDefaultAsync method.
    /// </summary>
Task<Result<SiteViewModel, AeroError>> GetDefaultAsync(CancellationToken ct = default);
        /// <summary>
    /// CreateAsync method.
    /// </summary>
Task<Result<SiteViewModel, AeroError>> CreateAsync(CreateSiteRequest request, CancellationToken ct = default);
        /// <summary>
    /// UpdateAsync method.
    /// </summary>
Task<Result<SiteViewModel, AeroError>> UpdateAsync(long id, UpdateSiteRequest request, CancellationToken ct = default);
        /// <summary>
    /// Updates a site's framework-neutral style profile.
    /// </summary>
Task<Result<SiteStyleProfileViewModel, AeroError>> UpdateStyleProfileAsync(
    long id,
    UpdateSiteStyleProfileRequest request,
    CancellationToken ct = default);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Represents a class for SitesHttpClient.
/// </summary>
public class SitesHttpClient(HttpClient httpClient, ILogger<SitesHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), ISitesHttpClient, Aero.Cms.Contracts.Abstractions.ISitesHttpClient
{
        /// <summary>
    /// Gets or sets the Path.
    /// </summary>
public override string Path => "admin/sites";

        /// <summary>
    /// GetAllAsync method.
    /// </summary>
public Task<Result<IReadOnlyList<SiteViewModel>, AeroError>> GetAllAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<SiteViewModel>>("", ct);

        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
public Task<Result<SiteViewModel, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
        => GetAsync<SiteViewModel>(id.ToString(), ct);

        /// <summary>
    /// GetDefaultAsync method.
    /// </summary>
public Task<Result<SiteViewModel, AeroError>> GetDefaultAsync(CancellationToken ct = default)
        => GetAsync<SiteViewModel>("default", ct);

        /// <summary>
    /// CreateAsync method.
    /// </summary>
public Task<Result<SiteViewModel, AeroError>> CreateAsync(CreateSiteRequest request, CancellationToken ct = default)
        => PostAsync<CreateSiteRequest, SiteViewModel>(string.Empty, request, ct);

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
public Task<Result<SiteViewModel, AeroError>> UpdateAsync(long id, UpdateSiteRequest request, CancellationToken ct = default)
        => PutAsync<UpdateSiteRequest, SiteViewModel>(id.ToString(), request, ct);

        /// <summary>
    /// Updates a site's framework-neutral style profile.
    /// </summary>
public Task<Result<SiteStyleProfileViewModel, AeroError>> UpdateStyleProfileAsync(
    long id,
    UpdateSiteStyleProfileRequest request,
    CancellationToken ct = default)
        => PutAsync<UpdateSiteStyleProfileRequest, SiteStyleProfileViewModel>(
            $"{id}/style-profile",
            request,
            ct);

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
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
        vm.Id, vm.Name, vm.PrimaryHost, vm.IsEnabled, vm.DefaultCulture, vm.TenantId, vm.SupportedCultures);

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
