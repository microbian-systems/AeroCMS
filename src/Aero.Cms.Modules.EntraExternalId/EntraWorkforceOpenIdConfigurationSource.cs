using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Aero.Cms.Modules.EntraExternalId;

public interface IEntraWorkforceOpenIdConfigurationSource
{
    Task<OpenIdConnectConfiguration> GetAsync(
        string authority,
        string organizationId,
        bool refresh,
        CancellationToken cancellationToken);
}

internal sealed class EntraWorkforceOpenIdConfigurationSource(EntraWorkforceHttpClient http)
    : IEntraWorkforceOpenIdConfigurationSource
{
    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _managers =
        new(StringComparer.Ordinal);

    public async Task<OpenIdConnectConfiguration> GetAsync(
        string authority,
        string organizationId,
        bool refresh,
        CancellationToken cancellationToken)
    {
        if (!EntraWorkforceEndpointRules.IsCanonicalAuthority(authority, organizationId))
            throw new IOException("The Entra Workforce authority was rejected.");

        if (refresh)
        {
            var replacement = Create(authority, organizationId);
            _managers.AddOrUpdate(authority, replacement, (_, _) => replacement);
            return await replacement.GetConfigurationAsync(cancellationToken);
        }

        var manager = _managers.GetOrAdd(authority, _ => Create(authority, organizationId));
        return await manager.GetConfigurationAsync(cancellationToken);
    }

    private ConfigurationManager<OpenIdConnectConfiguration> Create(string authority, string organizationId)
    {
        var metadataAddress = EntraWorkforceEndpointRules.Metadata(authority);
        return new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new EntraWorkforceBoundedDocumentRetriever(
                http,
                metadataAddress,
                EntraWorkforceEndpointRules.Jwks(organizationId),
                EntraWorkforceEndpointRules.CommonJwks));
    }
}

internal sealed class EntraWorkforceBoundedDocumentRetriever(
    EntraWorkforceHttpClient http,
    string metadataAddress,
    string tenantJwksAddress,
    string commonJwksAddress) : IDocumentRetriever
{
    public Task<string> GetDocumentAsync(string address, CancellationToken cancel)
    {
        if (!string.Equals(address, metadataAddress, StringComparison.Ordinal) &&
            !string.Equals(address, tenantJwksAddress, StringComparison.Ordinal) &&
            !string.Equals(address, commonJwksAddress, StringComparison.Ordinal))
            throw new IOException("The Entra Workforce document address was rejected.");

        return http.GetDocumentAsync(address, cancel);
    }
}

public sealed class EntraWorkforceHttpClient(HttpClient client)
{
    internal const int MaxDocumentBytes = 1024 * 1024;

    internal Task<string> GetDocumentAsync(string address, CancellationToken cancellationToken) =>
        SendForStringAsync(new HttpRequestMessage(HttpMethod.Get, address), cancellationToken);

    internal async Task<EntraWorkforceTokenResponse?> RedeemCodeAsync(
        string endpoint,
        string clientId,
        string clientSecret,
        string code,
        string verifier,
        string callbackUri,
        CancellationToken cancellationToken)
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
            return JsonSerializer.Deserialize(
                buffer.AsSpan(0, count), EntraWorkforceManagerJsonContext.Default.EntraWorkforceTokenResponse);
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
        using (var response = await client.SendAsync(
                   request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            RejectResponse(response);
            var buffer = new byte[MaxDocumentBytes];
            try
            {
                await using var stream = await response.Content!.ReadAsStreamAsync(cancellationToken);
                var count = 0;
                while (true)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(count), cancellationToken);
                    if (read == 0) return (buffer, count);
                    count += read;
                    if (count == buffer.Length &&
                        await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken) != 0)
                        throw new IOException("The Entra Workforce response was too large.");
                }
            }
            catch
            {
                CryptographicOperations.ZeroMemory(buffer);
                throw;
            }
        }
    }

    private async Task<string> SendForStringAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using (request)
        using (var response = await client.SendAsync(
                   request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            RejectResponse(response);
            await using var stream = await response.Content!.ReadAsStreamAsync(cancellationToken);
            using var memory = new MemoryStream();
            var buffer = new byte[81920];
            while (true)
            {
                var count = await stream.ReadAsync(buffer, cancellationToken);
                if (count == 0) break;
                if (memory.Length + count > MaxDocumentBytes)
                    throw new IOException("The Entra Workforce response was too large.");
                await memory.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }
            return new UTF8Encoding(false, true).GetString(
                memory.GetBuffer(), 0, checked((int)memory.Length));
        }
    }

    private static void RejectResponse(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode || response.Content is null ||
            !string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json",
                StringComparison.OrdinalIgnoreCase) ||
            response.Content.Headers.ContentLength is > MaxDocumentBytes)
            throw new HttpRequestException("The Entra Workforce response was rejected.");
    }
}

internal static class EntraWorkforceEndpointRules
{
    internal const string Host = "login.microsoftonline.com";
    internal const string CommonJwks = "https://login.microsoftonline.com/common/discovery/v2.0/keys";

    internal static string Metadata(string authority) => authority + "/.well-known/openid-configuration";
    internal static string Authorization(string organizationId) => Prefix(organizationId) + "/oauth2/v2.0/authorize";
    internal static string Token(string organizationId) => Prefix(organizationId) + "/oauth2/v2.0/token";
    internal static string Jwks(string organizationId) => Prefix(organizationId) + "/discovery/v2.0/keys";

    internal static bool Validate(
        OpenIdConnectConfiguration configuration,
        string authority,
        string issuer,
        string organizationId) =>
        IsCanonicalAuthority(authority, organizationId) &&
        string.Equals(configuration.Issuer, issuer, StringComparison.Ordinal) &&
        string.Equals(configuration.AuthorizationEndpoint, Authorization(organizationId), StringComparison.Ordinal) &&
        string.Equals(configuration.TokenEndpoint, Token(organizationId), StringComparison.Ordinal) &&
        (string.Equals(configuration.JwksUri, Jwks(organizationId), StringComparison.Ordinal) ||
         string.Equals(configuration.JwksUri, CommonJwks, StringComparison.Ordinal));

    internal static bool IsCanonicalAuthority(string? authority, string? organizationId) =>
        CanonicalTenant(organizationId) &&
        string.Equals(authority, $"https://{Host}/{organizationId}/v2.0", StringComparison.Ordinal);

    internal static bool CanonicalTenant(string? organizationId) =>
        organizationId is not null &&
        Guid.TryParseExact(organizationId, "D", out var parsed) &&
        string.Equals(organizationId, parsed.ToString("D").ToLowerInvariant(), StringComparison.Ordinal);

    private static string Prefix(string organizationId) => $"https://{Host}/{organizationId}";
}
