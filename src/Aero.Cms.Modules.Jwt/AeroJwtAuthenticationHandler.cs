using System.Security.Claims;
using System.Text.Encodings.Web;
using Aero.Auth.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Modules.Jwt;

/// <summary>
/// Validates bearer tokens through Aero's signing-key store instead of a second,
/// unrelated static signing secret.
/// </summary>
public sealed class AeroJwtAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IJwtTokenService jwtTokenService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Bearer";
    private const string Prefix = "Bearer ";
    private const int MaximumTokenLength = 16_384;

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = authorization[Prefix.Length..].Trim();
        if (string.IsNullOrEmpty(token) || token.Length > MaximumTokenLength)
            return AuthenticateResult.Fail("The bearer token is invalid.");

        var validation = await jwtTokenService.ValidateAccessTokenAsync(
            token,
            Context.RequestAborted);
        if (!validation.IsValid || validation.Principal is null)
            return AuthenticateResult.Fail("The bearer token is invalid.");

        var identity = new ClaimsIdentity(
            validation.Principal.Claims,
            Scheme.Name,
            ClaimTypes.Name,
            ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
