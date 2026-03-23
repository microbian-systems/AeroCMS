namespace Aero.Cms.Core.Http.Clients;

using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Abstract base class for typed HTTP clients used by Blazor WASM to communicate with the Admin API.
/// </summary>
public abstract class AeroClientBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    /// <summary>
    /// Gets the resource name for API endpoint construction (e.g., "blogs", "pages").
    /// </summary>
    protected abstract string ResourceName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AeroClientBase"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used for API requests.</param>
    /// <param name="logger">The logger instance.</param>
    protected AeroClientBase(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Sends a GET request to the specified resource path.
    /// </summary>
    /// <typeparam name="T">The expected response type.</typeparam>
    /// <param name="path">The resource path (relative to the resource base).</param>
    /// <param name="ct">A cancellation token to cancel the operation.</param>
    /// <returns>The deserialized response content, or default if the request fails.</returns>
    protected async Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/admin/{ResourceName}/{path}", ct);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GET request to {Path} failed with status {StatusCode}", path, response.StatusCode);
            return default;
        }
        
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    /// <summary>
    /// Sends a POST request with the specified content to the resource path.
    /// </summary>
    /// <typeparam name="T">The expected response type.</typeparam>
    /// <typeparam name="TRequest">The type of the request content.</typeparam>
    /// <param name="path">The resource path.</param>
    /// <param name="content">The request content to send.</param>
    /// <param name="ct">A cancellation token to cancel the operation.</param>
    /// <returns>The deserialized response content, or default if the request fails.</returns>
    protected async Task<T?> PostAsync<T, TRequest>(string path, TRequest content, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/v1/admin/{ResourceName}/{path}", content, ct);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("POST request to {Path} failed with status {StatusCode}", path, response.StatusCode);
            return default;
        }
        
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    /// <summary>
    /// Sends a PUT request with the specified content to the resource path.
    /// </summary>
    /// <typeparam name="T">The expected response type.</typeparam>
    /// <typeparam name="TRequest">The type of the request content.</typeparam>
    /// <param name="path">The resource path.</param>
    /// <param name="content">The request content to send.</param>
    /// <param name="ct">A cancellation token to cancel the operation.</param>
    /// <returns>The deserialized response content, or default if the request fails.</returns>
    protected async Task<T?> PutAsync<T, TRequest>(string path, TRequest content, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/admin/{ResourceName}/{path}", content, ct);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("PUT request to {Path} failed with status {StatusCode}", path, response.StatusCode);
            return default;
        }
        
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    /// <summary>
    /// Sends a DELETE request to the specified resource path.
    /// </summary>
    /// <param name="path">The resource path.</param>
    /// <param name="ct">A cancellation token to cancel the operation.</param>
    /// <returns>True if the delete operation was successful; otherwise, false.</returns>
    protected async Task<bool> DeleteAsync(string path, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/admin/{ResourceName}/{path}", ct);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("DELETE request to {Path} failed with status {StatusCode}", path, response.StatusCode);
            return false;
        }
        
        return true;
    }
}
