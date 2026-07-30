using System.Security.Claims;
using System.Text.Encodings.Web;
using Aero.Cms.Abstractions.Security;
using Aero.Cms.Abstractions.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Modules.Security;

/// <summary>
/// Authenticates an Aero API key supplied through the dedicated header or the <c>ApiKey</c>
/// authorization scheme.
/// </summary>
public sealed class AeroApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyService apiKeyService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const int MaximumKeyLength = 2048;

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var apiKey = ReadApiKey(out var isAmbiguous);
        if (isAmbiguous)
            return AuthenticateResult.Fail("Supply an API key through exactly one authentication header.");
        if (apiKey is null)
            return AuthenticateResult.NoResult();
        if (apiKey.Length > MaximumKeyLength)
            return AuthenticateResult.Fail("The API key is invalid.");

        var validation = await apiKeyService.ValidateAsync(apiKey, Context.RequestAborted);
        if (validation is null)
            return AuthenticateResult.Fail("The API key is invalid.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, validation.UserId.ToString()),
            new("sub", validation.UserId.ToString()),
            new(AeroApiKeyClaimTypes.KeyId, validation.KeyId.ToString()),
            new(AeroApiKeyClaimTypes.CredentialKind, validation.CredentialKind.ToString()),
            new(AeroApiKeyClaimTypes.McpServer, validation.McpServer ? "true" : "false"),
            new(AeroApiKeyClaimTypes.Administrator, validation.IsAdministrator ? "true" : "false")
        };

        if (validation.TenantId > 0)
            claims.Add(new Claim(AeroApiKeyClaimTypes.TenantId, validation.TenantId.ToString()));
        claims.AddRange(validation.AllowedSiteIds.Select(siteId =>
            new Claim(AeroApiKeyClaimTypes.SiteId, siteId.ToString())));
        claims.AddRange(validation.Permissions.Select(permission =>
            new Claim(AeroApiKeyClaimTypes.Permission, permission)));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    private string? ReadApiKey(out bool isAmbiguous)
    {
        var dedicatedHeader = Request.Headers[AeroApiKeyAuthenticationDefaults.HeaderName].ToString().Trim();
        var authorization = Request.Headers.Authorization.ToString();
        var authorizationKey = authorization.StartsWith(
            AeroApiKeyAuthenticationDefaults.AuthorizationPrefix,
            StringComparison.OrdinalIgnoreCase)
            ? authorization[AeroApiKeyAuthenticationDefaults.AuthorizationPrefix.Length..].Trim()
            : string.Empty;

        isAmbiguous = !string.IsNullOrEmpty(dedicatedHeader) &&
                      !string.IsNullOrEmpty(authorizationKey);
        if (isAmbiguous)
            return null;
        return !string.IsNullOrEmpty(dedicatedHeader)
            ? dedicatedHeader
            : !string.IsNullOrEmpty(authorizationKey)
                ? authorizationKey
                : null;
    }
}
