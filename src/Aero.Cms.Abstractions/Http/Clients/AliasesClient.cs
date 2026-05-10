using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Abstractions.Http.Clients;

/// <summary>
/// HTTP client interface for alias management.
/// NOTE: Backend API endpoints for aliases need to be implemented — see Aero.Cms.Modules.Aliases.
/// </summary>
public interface IAliasHttpClient
{
    Task<Result<IReadOnlyList<AliasViewModel>, AeroError>> GetAllBySiteAsync(long siteId, CancellationToken ct = default);
    Task<Result<AliasViewModel, AeroError>> CreateAsync(CreateAliasRequest request, CancellationToken ct = default);
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// HTTP client for alias management API.
/// </summary>
public class AliasesHttpClient(HttpClient httpClient, ILogger<AliasesHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), IAliasHttpClient
{
    public override string Path => "admin/aliases";

    public Task<Result<IReadOnlyList<AliasViewModel>, AeroError>> GetAllBySiteAsync(long siteId, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<AliasViewModel>>($"?siteId={siteId}", ct);

    public Task<Result<AliasViewModel, AeroError>> CreateAsync(CreateAliasRequest request, CancellationToken ct = default)
        => PostAsync<CreateAliasRequest, AliasViewModel>(string.Empty, request, ct);

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
