using System.Collections.Concurrent;
using System.Text;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Aero.Cms.Modules.EntraExternalId;

public interface IEntraOpenIdConfigurationSource
{
    Task<OpenIdConnectConfiguration> GetAsync(string authority, string organizationId, bool refresh,
        CancellationToken cancellationToken);
}

internal sealed class EntraOpenIdConfigurationSource(EntraExternalIdHttpClient http) : IEntraOpenIdConfigurationSource
{
    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _managers =
        new(StringComparer.Ordinal);

    public async Task<OpenIdConnectConfiguration> GetAsync(string authority, string organizationId, bool refresh,
        CancellationToken cancellationToken)
    {
        var key = authority + "\n" + organizationId;
        if (refresh)
        {
            var replacement = Create(authority, organizationId);
            _managers.AddOrUpdate(key, replacement, (_, _) => replacement);
            return await replacement.GetConfigurationAsync(cancellationToken);
        }
        var manager = _managers.GetOrAdd(key, _ => Create(authority, organizationId));
        return await manager.GetConfigurationAsync(cancellationToken);
    }

    private ConfigurationManager<OpenIdConnectConfiguration> Create(string authority, string organizationId)
    {
        var metadataAddress = authority + "/.well-known/openid-configuration";
        return new ConfigurationManager<OpenIdConnectConfiguration>(metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new EntraBoundedDocumentRetriever(http, metadataAddress, EntraEndpointRules.Jwks(authority, organizationId)));
    }
}

internal sealed class EntraBoundedDocumentRetriever(
    EntraExternalIdHttpClient http,
    string metadataAddress,
    string jwksAddress) : IDocumentRetriever
{
    public Task<string> GetDocumentAsync(string address, CancellationToken cancel)
    {
        if (!string.Equals(address, metadataAddress, StringComparison.Ordinal) &&
            !string.Equals(address, jwksAddress, StringComparison.Ordinal))
            throw new IOException("OpenID Connect document address was rejected.");
        return http.GetDocumentAsync(address, cancel);
    }
}

public sealed class EntraExternalIdHttpClient(HttpClient client)
{
    internal const int MaxDocumentBytes = 1024 * 1024;

    internal Task<string> GetDocumentAsync(string address, CancellationToken cancellationToken) =>
        SendForStringAsync(new HttpRequestMessage(HttpMethod.Get, address), cancellationToken);

    internal async Task<EntraTokenResponse?> RedeemCodeAsync(string endpoint, string clientId, string clientSecret,
        string code, string verifier, string callbackUri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["code_verifier"] = verifier,
                ["redirect_uri"] = callbackUri
            })
        };
        var (buffer, count) = await SendForBufferAsync(request, cancellationToken);
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize(
                buffer.AsSpan(0, count), EntraExternalIdJsonContext.Default.EntraTokenResponse);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private async Task<(byte[] Buffer, int Count)> SendForBufferAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using (request)
        using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                   cancellationToken))
        {
            if (!response.IsSuccessStatusCode || response.Content is null ||
                !string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json",
                    StringComparison.OrdinalIgnoreCase) ||
                response.Content.Headers.ContentLength is > MaxDocumentBytes)
                throw new HttpRequestException("The external identity response was rejected.");

            var buffer = new byte[MaxDocumentBytes];
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var count = 0;
                int read;
                while ((read = await stream.ReadAsync(buffer.AsMemory(count), cancellationToken)) > 0)
                {
                    count += read;
                    if (count == buffer.Length &&
                        await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken) != 0)
                        throw new IOException("The external identity response was too large.");
                }
                return (buffer, count);
            }
            catch
            {
                CryptographicOperations.ZeroMemory(buffer);
                throw;
            }
        }
    }

    private async Task<string> SendForStringAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            if (!response.IsSuccessStatusCode || response.Content is null ||
                !string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
                response.Content.Headers.ContentLength is > MaxDocumentBytes)
                throw new HttpRequestException("The external identity response was rejected.");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var memory = new MemoryStream();
            var buffer = new byte[81920];
            int count;
            while ((count = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (memory.Length + count > MaxDocumentBytes)
                    throw new IOException("The external identity response was too large.");
                await memory.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }
            return new UTF8Encoding(false, true).GetString(memory.GetBuffer(), 0, checked((int)memory.Length));
        }
    }
}

internal static class EntraEndpointRules
{
    public static string Authorization(string authority, string organizationId) =>
        Prefix(authority, organizationId) + "/oauth2/v2.0/authorize";
    public static string Token(string authority, string organizationId) =>
        Prefix(authority, organizationId) + "/oauth2/v2.0/token";
    public static string Jwks(string authority, string organizationId) =>
        Prefix(authority, organizationId) + "/discovery/v2.0/keys";
    public static string Logout(string authority, string organizationId) =>
        Prefix(authority, organizationId) + "/oauth2/v2.0/logout";

    public static bool Validate(OpenIdConnectConfiguration configuration, string authority, string issuer,
        string organizationId) =>
        string.Equals(configuration.Issuer, issuer, StringComparison.Ordinal) &&
        string.Equals(configuration.AuthorizationEndpoint, Authorization(authority, organizationId), StringComparison.Ordinal) &&
        string.Equals(configuration.TokenEndpoint, Token(authority, organizationId), StringComparison.Ordinal) &&
        string.Equals(configuration.JwksUri, Jwks(authority, organizationId), StringComparison.Ordinal) &&
        (string.IsNullOrEmpty(configuration.EndSessionEndpoint) ||
         string.Equals(configuration.EndSessionEndpoint, Logout(authority, organizationId), StringComparison.Ordinal));

    private static string Prefix(string authority, string organizationId) =>
        authority[..^("/" + organizationId + "/v2.0").Length] + "/" + organizationId;
}
