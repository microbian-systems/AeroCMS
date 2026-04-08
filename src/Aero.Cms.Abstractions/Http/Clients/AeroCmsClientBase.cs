namespace Aero.Cms.Core.Http.Clients;

using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base class for Aero CMS administration HTTP clients.
/// Provides standardized URL building and result handling for admin endpoints.
/// </summary>
public abstract class AeroCmsClientBase(
    HttpClient httpClient,
    ILogger<AeroCmsClientBase> logger) 
    : HttpClientBase(httpClient, logger)
{
    /// <summary>
    /// Gets the resource name used for URL building (e.g., "pages", "users").
    /// </summary>
    protected abstract string ResourceName { get; }

    /// <summary>
    /// Builds a full URL for the request.
    /// </summary>
    /// <param name="path">The optional relative path or query string.</param>
    /// <returns>The constructed URL.</returns>
    protected string BuildUrl(string? path)
    {
        var basePath = $"/api/v1/admin/{ResourceName}";
        if (string.IsNullOrEmpty(path)) return basePath;
        if (path.StartsWith("?")) return $"{basePath}{path}";
        
        // Handle absolute-like paths from subclasses (hacks like ../)
        if (path.StartsWith("../")) return $"/api/v1/admin/{path.Replace("../", "")}";

        return $"{basePath}/{path}";
    }

    /// <summary>
    /// Executes a GET request to the derived resource and return the response message.
    /// </summary>
    /// <param name="path">The relative path.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The HTTP response message or an error.</returns>
    protected virtual new Task<Result<HttpResponseMessage, AeroError>> GetAsync(string? path = null, CancellationToken ct = default)
        => base.GetAsync(BuildUrl(path), ct);

    /// <summary>
    /// Executes a POST request with data to the derived resource.
    /// </summary>
    /// <typeparam name="T">The type of data to post.</typeparam>
    /// <param name="path">The relative path.</param>
    /// <param name="data">The data to post.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The HTTP response message or an error.</returns>
    protected virtual new Task<Result<HttpResponseMessage, AeroError>> PostAsync<T>(string? path, T data, CancellationToken ct = default)
        where T : class
        => base.PostAsync(BuildUrl(path), data, ct);

    /// <summary>
    /// Executes a PUT request with data to the derived resource.
    /// </summary>
    /// <typeparam name="T">The type of data to put.</typeparam>
    /// <param name="path">The relative path.</param>
    /// <param name="data">The data to put.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The HTTP response message or an error.</returns>
    protected virtual new Task<Result<HttpResponseMessage, AeroError>> PutAsync<T>(string? path, T data, CancellationToken ct = default)
        where T : class
        => base.PutAsync(BuildUrl(path), data, ct);

    /// <summary>
    /// Executes a PATCH request with data to the derived resource.
    /// </summary>
    /// <typeparam name="T">The type of data to patch.</typeparam>
    /// <param name="path">The relative path.</param>
    /// <param name="data">The data to patch.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The HTTP response message or an error.</returns>
    protected virtual new Task<Result<HttpResponseMessage, AeroError>> PatchAsync<T>(string? path, T data, CancellationToken ct = default)
        where T : class
        => base.PatchAsync(BuildUrl(path), data, ct);

    /// <summary>
    /// Executes a DELETE request to the derived resource.
    /// </summary>
    /// <param name="path">The relative path.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The HTTP response message or an error.</returns>
    protected virtual new Task<Result<HttpResponseMessage, AeroError>> DeleteAsync(string? path, CancellationToken ct = default)
        => base.DeleteAsync(BuildUrl(path), ct);

    /// <summary>
    /// Executes an OPTION request to the derived resource.
    /// </summary>
    /// <param name="path">The relative path.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The HTTP response message or an error.</returns>
    protected virtual new Task<Result<HttpResponseMessage, AeroError>> OptionAsync(string? path = null, CancellationToken ct = default)
        => base.OptionAsync(BuildUrl(path), ct);

    /// <summary>
    /// Executes a GET request and deserializes the result.
    /// </summary>
    /// <typeparam name="T">The type of result data.</typeparam>
    /// <param name="path">The relative path.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The deserialized result or an error.</returns>
    protected virtual new Task<Result<T, AeroError>> GetAsync<T>(string? path = null, CancellationToken ct = default)
        => base.GetAsync<T>(BuildUrl(path), ct);

    /// <summary>
    /// Executes a POST request and deserializes the result.
    /// </summary>
    /// <typeparam name="TRequest">The type of request data.</typeparam>
    /// <typeparam name="TResponse">The type of response data.</typeparam>
    /// <param name="path">The relative path.</param>
    /// <param name="data">The request data.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The deserialized response or an error.</returns>
    protected virtual new Task<Result<TResponse, AeroError>> PostAsync<TRequest, TResponse>(string? path, TRequest data, CancellationToken ct = default)
        where TRequest : class
        => base.PostAsync<TRequest, TResponse>(BuildUrl(path), data, ct);

    /// <summary>
    /// Executes a PUT request and deserializes the result.
    /// </summary>
    /// <typeparam name="TRequest">The type of request data.</typeparam>
    /// <typeparam name="TResponse">The type of response data.</typeparam>
    /// <param name="path">The relative path.</param>
    /// <param name="data">The request data.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The deserialized response or an error.</returns>
    protected virtual new Task<Result<TResponse, AeroError>> PutAsync<TRequest, TResponse>(string? path, TRequest data, CancellationToken ct = default)
        where TRequest : class
        => base.PutAsync<TRequest, TResponse>(BuildUrl(path), data, ct);

    /// <summary>
    /// Executes a PATCH request and deserializes the result.
    /// </summary>
    /// <typeparam name="TRequest">The type of request data.</typeparam>
    /// <typeparam name="TResponse">The type of response data.</typeparam>
    /// <param name="path">The relative path.</param>
    /// <param name="data">The request data.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The deserialized response or an error.</returns>
    protected virtual new Task<Result<TResponse, AeroError>> PatchAsync<TRequest, TResponse>(string? path, TRequest data, CancellationToken ct = default)
        where TRequest : class
        => base.PatchAsync<TRequest, TResponse>(BuildUrl(path), data, ct);

    /// <summary>
    /// Executes a DELETE request and returns success state.
    /// </summary>
    /// <param name="path">The relative path.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if successful or an error.</returns>
    protected virtual new Task<Result<bool, AeroError>> DeleteAsync(string? path, CancellationToken ct = default)
        => base.DeleteAsync(BuildUrl(path), ct);

    /// <summary>
    /// Executes a GET request to retrieve binary data.
    /// </summary>
    /// <param name="path">The relative path.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The HTTP response message or an error.</returns>
    protected virtual new Task<Result<HttpResponseMessage, AeroError>> GetBinaryAsync(string? path = null, CancellationToken ct = default)
        => base.GetBinaryAsync(BuildUrl(path), ct);

    /// <summary>
    /// Downloads binary data as a byte array.
    /// </summary>
    /// <param name="path">The relative path.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The byte array or an error.</returns>
    protected virtual new Task<Result<byte[]?, AeroError>> DownloadBytesAsync(string? path = null, CancellationToken ct = default)
        => base.DownloadBytesAsync(BuildUrl(path), ct);

    /// <summary>
    /// Downloads binary data as a stream.
    /// </summary>
    /// <param name="path">The relative path.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The stream or an error.</returns>
    protected virtual new Task<Result<Stream?, AeroError>> DownloadStreamAsync(string? path = null, CancellationToken ct = default)
        => base.DownloadStreamAsync(BuildUrl(path), ct);

    /// <summary>
    /// Sends a general request and deserializes the result.
    /// </summary>
    /// <typeparam name="T">The type of result data.</typeparam>
    /// <param name="request">The HTTP request message.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The deserialized result or an error.</returns>
    protected virtual new Task<Result<T, AeroError>> SendResultAsync<T>(HttpRequestMessage request, CancellationToken ct = default)
        => base.SendResultAsync<T>(request, ct);

    /// <summary>
    /// Sends a request and returns both the deserialized result and original response.
    /// </summary>
    /// <typeparam name="T">The type of result data.</typeparam>
    /// <param name="request">The HTTP request message.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The tuple of result and response message, or an error.</returns>
    protected virtual new Task<Result<(T result, HttpResponseMessage response), AeroError>> SendRequestAsync<T>(HttpRequestMessage request, CancellationToken ct = default) 
        where T : class
        => base.SendRequestAsync<T>(request, ct);
}