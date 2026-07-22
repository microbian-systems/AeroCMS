using System.Net.Http.Json;
using Aero.Cms.Abstractions.Authentication;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Web.Client.Services;

/// <summary>
/// Resolves the server-authoritative manager authentication mode for the browser client.
/// </summary>
internal sealed class HttpManagerAuthenticationModeResolver(HttpClient httpClient)
    : IManagerAuthenticationModeResolver
{
    private const string ConfigurationPath = "/api/v1/admin/auth/config";

    public async Task<Result<ManagerAuthenticationModeResolution, AeroError>> ResolveAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync(ConfigurationPath, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Prelude.Fail<ManagerAuthenticationModeResolution, AeroError>(
                    AeroError.HttpRequestError(
                        response.StatusCode,
                        "Manager authentication mode is unavailable."));
            }

            var resolution = await response.Content.ReadFromJsonAsync<ManagerAuthenticationModeResolution>(
                cancellationToken: cancellationToken);

            return resolution is not null
                ? Prelude.Ok<ManagerAuthenticationModeResolution, AeroError>(resolution)
                : Prelude.Fail<ManagerAuthenticationModeResolution, AeroError>(
                    AeroError.CreateError("Manager authentication mode response was empty."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Prelude.Fail<ManagerAuthenticationModeResolution, AeroError>(
                AeroError.CreateError("Manager authentication mode could not be loaded."));
        }
    }
}
