namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Cms.Abstractions.Ai;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for AeroCMS AI manager endpoints.
/// </summary>
public interface IAiHttpClient
{
    /// <summary>
    /// Enhances one content field and returns a suggestion for review.
    /// </summary>
    Task<Result<EnhanceContentResponse, AeroError>> EnhanceContentAsync(
        EnhanceContentRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Typed client for AI manager endpoints.
/// </summary>
public sealed class AiHttpClient(HttpClient httpClient, ILogger<AiHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), IAiHttpClient
{
    /// <inheritdoc />
    public override string Path => "admin/ai";

    /// <inheritdoc />
    public Task<Result<EnhanceContentResponse, AeroError>> EnhanceContentAsync(
        EnhanceContentRequest request,
        CancellationToken ct = default)
    {
        return PostAsync<EnhanceContentRequest, EnhanceContentResponse>("content/enhance", request, ct);
    }
}
