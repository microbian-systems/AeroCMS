using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Aero.Cms.Core.Http.Clients;

public abstract class AeroCmsClientBase(
    HttpClient httpClient,
    ILogger<AeroCmsClientBase> logger) 
    : HttpClientBase(httpClient, logger)
{
    protected abstract string ResourceName { get; }

    protected string BuildUrl(string? path)
    {
        var basePath = $"/api/v1/admin/{ResourceName}";
        return string.IsNullOrEmpty(path) ? basePath : $"{basePath}/{path}";
    }

    protected new Task<Result<string, T>> GetResultAsync<T>(string? path = null, CancellationToken ct = default)
        => base.GetResultAsync<T>(BuildUrl(path), ct);

    protected new Task<Result<string, TResponse>> PostResultAsync<TRequest, TResponse>(string? path, TRequest data, CancellationToken ct = default)
        where TRequest : class
        => base.PostResultAsync<TRequest, TResponse>(BuildUrl(path), data, ct);

    protected new Task<Result<string, TResponse>> PutResultAsync<TRequest, TResponse>(string? path, TRequest data, CancellationToken ct = default)
        where TRequest : class
        => base.PutResultAsync<TRequest, TResponse>(BuildUrl(path), data, ct);

    protected new Task<Result<string, bool>> DeleteResultAsync(string? path, CancellationToken ct = default)
        => base.DeleteResultAsync(BuildUrl(path), ct);
}