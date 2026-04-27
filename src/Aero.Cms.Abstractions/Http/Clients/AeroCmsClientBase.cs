namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Aero.Cms.Abstractions.Http.Clients;

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
    public abstract string Path { get; }

    /// <summary>
    /// Builds the full request URI by auto-prefixing relative URLs with "api/v1/{Path}".
    /// Already-prefixed or absolute URLs are passed through unchanged.
    /// This ensures all Aero CMS clients consistently target the correct API root.
    /// </summary>
    protected override Uri CreateUri(string url)
    {
        // Pass through if already prefixed or absolute
        if (url.StartsWith(HttpConstants.ApiPrefix, StringComparison.Ordinal) ||
            Uri.TryCreate(url, UriKind.Absolute, out _))
            return base.CreateUri(url);

        // Auto-prefix relative URLs: "details/42" → "api/v1/{path}/details/42"
        var prefixed = url switch
        {
            "" => $"{HttpConstants.ApiPrefix}{Path}",
            _ when url.StartsWith('?') => $"{HttpConstants.ApiPrefix}{Path}{url}",
            _ => $"{HttpConstants.ApiPrefix}{Path}/{url.TrimStart('/')}"
        };

        return base.CreateUri(prefixed);
    }
}
