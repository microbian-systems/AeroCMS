using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aero.Cms.Abstractions.Authentication;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Aero.Cms.Modules.EntraExternalId;

/// <summary>A direct, cookie-free OpenID Connect adapter for Microsoft Entra External ID.</summary>
public sealed class EntraExternalIdProviderStrategy : IExternalMemberProviderStrategy
{
    private static readonly TimeSpan CorrelationLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TokenClockSkew = TimeSpan.FromMinutes(1);
    private readonly ITimeLimitedDataProtector _protector;
    private readonly IEntraOpenIdConfigurationSource _configuration;
    private readonly EntraExternalIdHttpClient _http;
    private readonly TimeProvider _time;
    private readonly JsonWebTokenHandler _tokens = new() { MapInboundClaims = false, MaximumTokenSizeInBytes = 32 * 1024 };

    public EntraExternalIdProviderStrategy(IDataProtectionProvider protection,
        IEntraOpenIdConfigurationSource configuration, EntraExternalIdHttpClient http, TimeProvider time)
    {
        _protector = protection.CreateProtector("AeroCms.ExternalMember.EntraExternalId.Correlation.v1")
            .ToTimeLimitedDataProtector();
        _configuration = configuration;
        _http = http;
        _time = time;
    }

    public string Provider => ExternalMemberProviders.EntraExternalId;

    public Task<Result<ExternalProviderAuthorizationPreparation, AeroError>> PrepareAuthorizationAsync(
        ExternalProviderBeginContext context, ExternalProviderCredentialBundle credentials,
        CancellationToken cancellationToken = default)
    {
        if (!ValidContext(context) || !TryCredential(credentials.LeaseClientId().Bytes, 512, out var clientId))
            return Task.FromResult(Fail<ExternalProviderAuthorizationPreparation>());

        Span<byte> verifierBytes = stackalloc byte[32];
        Span<byte> nonceBytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(verifierBytes);
        RandomNumberGenerator.Fill(nonceBytes);
        var correlation = new EntraCorrelation(context.Authority.BindingId, context.Authority.TenantId, context.SiteId,
            context.CallbackUri.AbsoluteUri, context.Authority.Authority, Digest(clientId),
            WebEncoders.Base64UrlEncode(nonceBytes), WebEncoders.Base64UrlEncode(verifierBytes));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(correlation, EntraExternalIdJsonContext.Default.EntraCorrelation);
        var protectedValue = WebEncoders.Base64UrlEncode(_protector.Protect(bytes, CorrelationLifetime));
        return Task.FromResult(Prelude.Ok<ExternalProviderAuthorizationPreparation, AeroError>(new(protectedValue)));
    }

    public async Task<Result<ExternalProviderAuthorizationChallenge, AeroError>> CreateAuthorizationAsync(
        ExternalProviderBeginContext context, ExternalProviderAuthorizationPreparation preparation,
        string authenticationHandle, ExternalProviderCredentialBundle credentials,
        CancellationToken cancellationToken = default)
    {
        if (!ValidContext(context) || !ValidHandle(authenticationHandle) ||
            !TryCredential(credentials.LeaseClientId().Bytes, 512, out var clientId) ||
            !TryCorrelation(preparation.ProtectedProviderCorrelation, context, clientId, out var correlation))
            return Fail<ExternalProviderAuthorizationChallenge>();
        try
        {
            var configuration = await _configuration.GetAsync(context.Authority.Authority,
                context.Authority.OrganizationId, false, cancellationToken);
            if (!EntraEndpointRules.Validate(configuration, context.Authority.Authority,
                    context.Authority.Issuer, context.Authority.OrganizationId))
                return Fail<ExternalProviderAuthorizationChallenge>();

            var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(correlation.Verifier)));
            var parameters = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["response_type"] = "code",
                ["redirect_uri"] = context.CallbackUri.AbsoluteUri,
                ["response_mode"] = "query",
                ["scope"] = "openid profile email",
                ["state"] = authenticationHandle,
                ["nonce"] = correlation.Nonce,
                ["code_challenge"] = challenge,
                ["code_challenge_method"] = "S256"
            };
            var target = QueryHelpers.AddQueryString(configuration.AuthorizationEndpoint, parameters!);
            return Prelude.Ok<ExternalProviderAuthorizationChallenge, AeroError>(new(
                ExternalProviderAuthorizationChallengeKind.Redirect, target, new Dictionary<string, string>()));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Fail<ExternalProviderAuthorizationChallenge>(); }
    }

    public async Task<Result<ValidatedExternalIdentity, AeroError>> AuthenticateAsync(
        ExternalProviderCallbackContext context, ExternalProviderCredentialBundle credentials,
        CancellationToken cancellationToken = default)
    {
        if (context.Error is not null || !Opaque(context.Code, 2048) || !ValidContext(new(
                context.Authority, context.SiteId, context.CallbackUri, string.Empty)) ||
            !TryCredential(credentials.LeaseClientId().Bytes, 512, out var clientId) ||
            !TryCredential(credentials.LeaseClientSecret().Bytes, 2048, out var clientSecret) ||
            !TryCorrelation(context.ProtectedProviderCorrelation, new(context.Authority, context.SiteId,
                context.CallbackUri, string.Empty), clientId, out var correlation))
            return Fail<ValidatedExternalIdentity>();

        try
        {
            var configuration = await _configuration.GetAsync(context.Authority.Authority,
                context.Authority.OrganizationId, false, cancellationToken);
            if (!EntraEndpointRules.Validate(configuration, context.Authority.Authority,
                    context.Authority.Issuer, context.Authority.OrganizationId))
                return Fail<ValidatedExternalIdentity>();

            // Authorization codes are redeemed exactly once. This call is deliberately outside all retry paths.
            var response = await _http.RedeemCodeAsync(configuration.TokenEndpoint, clientId, clientSecret,
                context.Code!, correlation.Verifier, context.CallbackUri.AbsoluteUri, cancellationToken);
            if (response?.id_token is not { Length: > 0 and <= 32768 } idToken)
                return Fail<ValidatedExternalIdentity>();

            var validation = await ValidateTokenAsync(idToken, clientId, context.Authority.Issuer,
                configuration, cancellationToken);
            if (!validation.IsValid && validation.Exception is SecurityTokenSignatureKeyNotFoundException)
            {
                configuration = await _configuration.GetAsync(context.Authority.Authority,
                    context.Authority.OrganizationId, true, cancellationToken);
                if (!EntraEndpointRules.Validate(configuration, context.Authority.Authority,
                        context.Authority.Issuer, context.Authority.OrganizationId))
                    return Fail<ValidatedExternalIdentity>();
                validation = await ValidateTokenAsync(idToken, clientId, context.Authority.Issuer,
                    configuration, cancellationToken);
            }
            if (!validation.IsValid || validation.SecurityToken is not JsonWebToken jwt ||
                !string.Equals(jwt.Alg, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal) ||
                !TryReadIdentityPayload(idToken, clientId, context.Authority.OrganizationId, correlation.Nonce,
                    out var payload))
                return Fail<ValidatedExternalIdentity>();

            return Prelude.Ok<ValidatedExternalIdentity, AeroError>(new(Provider, context.Authority.Issuer,
                payload.Subject, context.Authority.OrganizationId, payload.Email, payload.EmailVerified,
                payload.DisplayName, null, _time.GetUtcNow()));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Fail<ValidatedExternalIdentity>(); }
    }

    public async Task<Result<ExternalProviderAuthorizationChallenge, AeroError>> PrepareLogoutAsync(
        ExternalProviderLogoutContext context, ExternalProviderCredentialBundle credentials,
        CancellationToken cancellationToken = default)
    {
        if (!ValidContext(new(context.Authority, context.SiteId, context.ReturnUri, string.Empty)) ||
            context.ReturnUri.AbsolutePath != "/")
            return Fail<ExternalProviderAuthorizationChallenge>();
        try
        {
            var configuration = await _configuration.GetAsync(context.Authority.Authority,
                context.Authority.OrganizationId, false, cancellationToken);
            if (!EntraEndpointRules.Validate(configuration, context.Authority.Authority,
                    context.Authority.Issuer, context.Authority.OrganizationId) ||
                !string.Equals(configuration.EndSessionEndpoint,
                    EntraEndpointRules.Logout(context.Authority.Authority, context.Authority.OrganizationId),
                    StringComparison.Ordinal))
                return Fail<ExternalProviderAuthorizationChallenge>();
            var values = new Dictionary<string, string>
            {
                ["post_logout_redirect_uri"] = context.ReturnUri.AbsoluteUri
            };
            return Prelude.Ok<ExternalProviderAuthorizationChallenge, AeroError>(new(
                ExternalProviderAuthorizationChallengeKind.Redirect,
                QueryHelpers.AddQueryString(configuration.EndSessionEndpoint, values!), values));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Fail<ExternalProviderAuthorizationChallenge>(); }
    }

    private Task<TokenValidationResult> ValidateTokenAsync(string token, string clientId, string issuer,
        OpenIdConnectConfiguration configuration, CancellationToken cancellationToken) =>
        _tokens.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
            RequireSignedTokens = true,
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = clientId,
            RequireAudience = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TokenClockSkew,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        });

    private bool TryCorrelation(string protectedValue, ExternalProviderBeginContext context, string clientId,
        out EntraCorrelation correlation)
    {
        correlation = default!;
        try
        {
            var clear = _protector.Unprotect(WebEncoders.Base64UrlDecode(protectedValue), out var expiresAt);
            var value = JsonSerializer.Deserialize(clear, EntraExternalIdJsonContext.Default.EntraCorrelation);
            if (value is null || expiresAt <= _time.GetUtcNow() || value.Verifier.Length != 43 ||
                value.Nonce.Length != 43 || value.BindingId != context.Authority.BindingId ||
                value.TenantId != context.Authority.TenantId || value.SiteId != context.SiteId ||
                !string.Equals(value.CallbackUri, context.CallbackUri.AbsoluteUri, StringComparison.Ordinal) ||
                !string.Equals(value.Authority, context.Authority.Authority, StringComparison.Ordinal) ||
                !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(value.ClientIdDigest),
                    Encoding.ASCII.GetBytes(Digest(clientId))))
                return false;
            correlation = value;
            return true;
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            return false;
        }
    }

    private bool TryReadIdentityPayload(string token, string clientId, string organizationId, string nonce,
        out EntraIdentityPayload payload)
    {
        payload = default!;
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3 || parts[1].Length > 24576)
                return false;
            using var document = JsonDocument.Parse(WebEncoders.Base64UrlDecode(parts[1]));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !ExactAudience(root, clientId) ||
                !String(root, "tid", 36, out var tid) || !string.Equals(tid, organizationId, StringComparison.Ordinal) ||
                !String(root, "sub", 512, out var subject) || !String(root, "nonce", 128, out var tokenNonce) ||
                !string.Equals(tokenNonce, nonce, StringComparison.Ordinal) ||
                !Integer(root, "iat", out var issuedAt))
                return false;

            var now = _time.GetUtcNow().ToUnixTimeSeconds();
            if (issuedAt > now + (long)TokenClockSkew.TotalSeconds || issuedAt < now - 7200)
                return false;
            if (root.TryGetProperty("nbf", out var nbf) &&
                (nbf.ValueKind != JsonValueKind.Number || !nbf.TryGetInt64(out var notBefore) ||
                 notBefore > now + (long)TokenClockSkew.TotalSeconds || notBefore < now - 7200))
                return false;

            string? email = null;
            var emailVerified = false;
            if (root.TryGetProperty("email_verified", out var verified))
            {
                emailVerified = verified.ValueKind == JsonValueKind.True;
            }
            if (emailVerified)
            {
                if (!String(root, "email", 320, out var value) || !ValidEmail(value))
                    return false;
                email = value;
            }
            var displayName = OptionalString(root, "name", 512);
            payload = new(subject, email, emailVerified, displayName);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            return false;
        }
    }

    private static bool ExactAudience(JsonElement root, string clientId)
    {
        if (!root.TryGetProperty("aud", out var audience)) return false;
        if (audience.ValueKind == JsonValueKind.String)
            return string.Equals(audience.GetString(), clientId, StringComparison.Ordinal);
        if (audience.ValueKind != JsonValueKind.Array || audience.GetArrayLength() != 1) return false;
        var single = audience[0];
        return single.ValueKind == JsonValueKind.String &&
               string.Equals(single.GetString(), clientId, StringComparison.Ordinal);
    }

    private static bool String(JsonElement root, string name, int max, out string value)
    {
        value = string.Empty;
        return root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String &&
               (value = property.GetString() ?? string.Empty) is { Length: > 0 } && value.Length <= max &&
               value == value.Trim() && !value.Any(char.IsControl);
    }

    private static bool Integer(JsonElement root, string name, out long value)
    {
        value = 0;
        return root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt64(out value);
    }

    private static string? OptionalString(JsonElement root, string name, int max) =>
        String(root, name, max, out var value) ? value : null;

    private static bool ValidContext(ExternalProviderBeginContext context) =>
        context.Authority.Provider == ExternalMemberProviders.EntraExternalId &&
        context.Authority.BindingId > 0 && context.Authority.TenantId > 0 && context.SiteId > 0 &&
        CanonicalOrganizationId(context.Authority.OrganizationId) && CanonicalAuthority(context.Authority) &&
        SafeHttps(context.CallbackUri);

    private static bool CanonicalOrganizationId(string value) =>
        Guid.TryParseExact(value, "D", out var parsed) &&
        string.Equals(value, parsed.ToString("D"), StringComparison.Ordinal);

    private static bool CanonicalAuthority(ExternalProviderAuthority authority)
    {
        var organizationId = authority.OrganizationId;
        var issuer = $"https://{organizationId}.ciamlogin.com/{organizationId}/v2.0";
        if (!string.Equals(authority.Issuer, issuer, StringComparison.Ordinal) ||
            !Uri.TryCreate(authority.Authority, UriKind.Absolute, out var uri) || !SafeHttps(uri) ||
            !uri.Host.EndsWith(".ciamlogin.com", StringComparison.Ordinal) ||
            !string.Equals(uri.AbsolutePath, $"/{organizationId}/v2.0", StringComparison.Ordinal) ||
            !string.Equals(uri.IdnHost, uri.Host, StringComparison.Ordinal))
            return false;
        var label = uri.Host[..^".ciamlogin.com".Length];
        return label is { Length: > 0 and <= 63 } && label[0] != '-' && label[^1] != '-' &&
               !label.StartsWith("xn--", StringComparison.Ordinal) &&
               label.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
    }

    private static bool SafeHttps(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps &&
        uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment);

    private static bool TryCredential(ReadOnlyMemory<byte> bytes, int max, out string value)
    {
        value = string.Empty;
        if (bytes.Length is 0 || bytes.Length > max) return false;
        try
        {
            value = new UTF8Encoding(false, true).GetString(bytes.Span);
            return Opaque(value, max);
        }
        catch (DecoderFallbackException) { return false; }
    }

    private static string Digest(string value) =>
        WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool ValidHandle(string value) => value is { Length: >= 46 and <= 64 } &&
        value.Count(character => character == '.') == 1 && !value.Any(char.IsControl);
    private static bool Opaque(string? value, int max) => value is { Length: > 0 } && value.Length <= max &&
        value == value.Trim() && !value.Any(char.IsControl);
    private static bool ValidEmail(string value) => value.Contains('@') && !value.Any(char.IsControl);
    private static Result<T, AeroError> Fail<T>() => Prelude.Fail<T, AeroError>(
        AeroError.CreateError("External sign-in is unavailable."));

    private sealed record EntraIdentityPayload(string Subject, string? Email, bool EmailVerified, string? DisplayName);
}
