namespace Aero.Cms.Core.Http.Clients;

using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using System.Diagnostics;
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
}
