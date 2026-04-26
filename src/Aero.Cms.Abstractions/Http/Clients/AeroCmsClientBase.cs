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

}
