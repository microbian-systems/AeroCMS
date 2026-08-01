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

/// <summary>Authenticates installation managers with Microsoft Entra Workforce.</summary>
public sealed class EntraWorkforceManagerIdentityProviderStrategy : IManagerIdentityProviderStrategy
{
    internal const string CallbackPath = ManagerFederationRoutes.EntraWorkforceCallbackPath;
    private static readonly TimeSpan CorrelationLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TokenClockSkew = TimeSpan.FromMinutes(1);
    private readonly ITimeLimitedDataProtector _protector;
    private readonly IEntraWorkforceOpenIdConfigurationSource _configuration;
    private readonly EntraWorkforceHttpClient _http;
    private readonly TimeProvider _time;
    private readonly JsonWebTokenHandler _tokens = new()
    {
        MapInboundClaims = false,
        MaximumTokenSizeInBytes = 32 * 1024
    };

    public EntraWorkforceManagerIdentityProviderStrategy(
        IDataProtectionProvider protection,
        IEntraWorkforceOpenIdConfigurationSource configuration,
        EntraWorkforceHttpClient http,
        TimeProvider time)
    {
        _protector = protection
            .CreateProtector("AeroCms.ManagerFederation.EntraWorkforce.Correlation.v1")
            .ToTimeLimitedDataProtector();
        _configuration = configuration;
        _http = http;
        _time = time;
    }

    public string Provider => ManagerIdentityProviders.EntraWorkforce;

    public Task<Result<ManagerProviderAuthorizationPreparation, AeroError>> PrepareAuthorizationAsync(
        ManagerProviderBeginContext context,
        ManagerProviderCredentialBundle credentials,
        CancellationToken cancellationToken = default)
    {
        if (!ValidContext(context) ||
            !TryCredential(credentials.LeaseClientId().Bytes, 512, out var clientId))
            return Task.FromResult(Fail<ManagerProviderAuthorizationPreparation>());

        Span<byte> verifierBytes = stackalloc byte[32];
        Span<byte> nonceBytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(verifierBytes);
        RandomNumberGenerator.Fill(nonceBytes);
        var correlation = new EntraWorkforceManagerCorrelation(
            context.Authority.BindingId,
            Provider,
            context.Authority.Authority,
            context.Authority.OrganizationId,
            context.CallbackUri.AbsoluteUri,
            context.Purpose,
            Digest(clientId),
            WebEncoders.Base64UrlEncode(nonceBytes),
            WebEncoders.Base64UrlEncode(verifierBytes));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            correlation, EntraWorkforceManagerJsonContext.Default.EntraWorkforceManagerCorrelation);
        try
        {
            var protectedValue = WebEncoders.Base64UrlEncode(
                _protector.Protect(bytes, CorrelationLifetime));
            return Task.FromResult(Prelude.Ok<ManagerProviderAuthorizationPreparation, AeroError>(
                new(protectedValue)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public async Task<Result<ManagerProviderAuthorizationChallenge, AeroError>> CreateAuthorizationAsync(
        ManagerProviderBeginContext context,
        ManagerProviderAuthorizationPreparation preparation,
        string stateHandle,
        ManagerProviderCredentialBundle credentials,
        CancellationToken cancellationToken = default)
    {
        if (!ValidContext(context) || !ValidStateHandle(stateHandle) ||
            !TryCredential(credentials.LeaseClientId().Bytes, 512, out var clientId) ||
            !TryCorrelation(preparation.ProtectedProviderCorrelation, context, clientId, out var correlation))
            return Fail<ManagerProviderAuthorizationChallenge>();

        try
        {
            var configuration = await _configuration.GetAsync(
                context.Authority.Authority, context.Authority.OrganizationId, false, cancellationToken);
            if (!EntraWorkforceEndpointRules.Validate(configuration, context.Authority.Authority,
                    context.Authority.Issuer, context.Authority.OrganizationId))
                return Fail<ManagerProviderAuthorizationChallenge>();

            var challenge = WebEncoders.Base64UrlEncode(
                SHA256.HashData(Encoding.ASCII.GetBytes(correlation.Verifier)));
            var parameters = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["response_type"] = "code",
                ["redirect_uri"] = context.CallbackUri.AbsoluteUri,
                ["response_mode"] = "query",
                ["scope"] = "openid profile",
                ["state"] = stateHandle,
                ["nonce"] = correlation.Nonce,
                ["code_challenge"] = challenge,
                ["code_challenge_method"] = "S256"
            };
            return Prelude.Ok<ManagerProviderAuthorizationChallenge, AeroError>(new(
                new Uri(QueryHelpers.AddQueryString(configuration.AuthorizationEndpoint, parameters!))));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Fail<ManagerProviderAuthorizationChallenge>();
        }
    }

    public async Task<Result<ValidatedManagerIdentity, AeroError>> AuthenticateAsync(
        ManagerProviderCallbackContext context,
        ManagerProviderCredentialBundle credentials,
        CancellationToken cancellationToken = default)
    {
        if (context.Error is not null || !Opaque(context.Code, 2048) || !ValidContext(context) ||
            !TryCredential(credentials.LeaseClientId().Bytes, 512, out var clientId) ||
            !TryCredential(credentials.LeaseClientSecret().Bytes, 2048, out var clientSecret) ||
            !TryCorrelation(context.ProtectedProviderCorrelation, context, clientId, out var correlation))
            return Fail<ValidatedManagerIdentity>();

        try
        {
            var configuration = await _configuration.GetAsync(
                context.Authority.Authority, context.Authority.OrganizationId, false, cancellationToken);
            if (!EntraWorkforceEndpointRules.Validate(configuration, context.Authority.Authority,
                    context.Authority.Issuer, context.Authority.OrganizationId))
                return Fail<ValidatedManagerIdentity>();

            // The authorization code is redeemed once and is never included in the signing-key retry path.
            var response = await _http.RedeemCodeAsync(
                configuration.TokenEndpoint,
                clientId,
                clientSecret,
                context.Code!,
                correlation.Verifier,
                context.CallbackUri.AbsoluteUri,
                cancellationToken);
            if (response?.id_token is not { Length: > 0 and <= 32768 } idToken)
                return Fail<ValidatedManagerIdentity>();

            var validation = await ValidateTokenAsync(
                idToken, clientId, context.Authority.Issuer, configuration);
            if (!validation.IsValid && validation.Exception is SecurityTokenSignatureKeyNotFoundException)
            {
                configuration = await _configuration.GetAsync(
                    context.Authority.Authority, context.Authority.OrganizationId, true, cancellationToken);
                if (!EntraWorkforceEndpointRules.Validate(configuration, context.Authority.Authority,
                        context.Authority.Issuer, context.Authority.OrganizationId))
                    return Fail<ValidatedManagerIdentity>();
                validation = await ValidateTokenAsync(
                    idToken, clientId, context.Authority.Issuer, configuration);
            }

            if (!validation.IsValid || validation.SecurityToken is not JsonWebToken jwt ||
                !string.Equals(jwt.Alg, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal) ||
                !TryReadIdentityPayload(
                    idToken, clientId, context.Authority.OrganizationId, correlation.Nonce,
                    out var subject, out var authenticatedAt))
                return Fail<ValidatedManagerIdentity>();

            return Prelude.Ok<ValidatedManagerIdentity, AeroError>(new(
                Provider,
                context.Authority.Issuer,
                subject,
                context.Authority.OrganizationId,
                null,
                authenticatedAt));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Fail<ValidatedManagerIdentity>();
        }
    }

    private Task<TokenValidationResult> ValidateTokenAsync(
        string token,
        string clientId,
        string issuer,
        OpenIdConnectConfiguration configuration) =>
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

    private bool TryCorrelation(
        string protectedValue,
        ManagerProviderBeginContext context,
        string clientId,
        out EntraWorkforceManagerCorrelation correlation)
    {
        correlation = default!;
        try
        {
            var clear = _protector.Unprotect(
                WebEncoders.Base64UrlDecode(protectedValue), out var expiresAt);
            try
            {
                var value = JsonSerializer.Deserialize(
                    clear, EntraWorkforceManagerJsonContext.Default.EntraWorkforceManagerCorrelation);
                if (value is null || expiresAt <= _time.GetUtcNow() || value.Verifier.Length != 43 ||
                    value.Nonce.Length != 43 || value.BindingId != context.Authority.BindingId ||
                    !string.Equals(value.Provider, Provider, StringComparison.Ordinal) ||
                    !string.Equals(value.Authority, context.Authority.Authority, StringComparison.Ordinal) ||
                    !string.Equals(value.OrganizationId, context.Authority.OrganizationId, StringComparison.Ordinal) ||
                    !string.Equals(value.CallbackUri, context.CallbackUri.AbsoluteUri, StringComparison.Ordinal) ||
                    !string.Equals(value.Purpose, context.Purpose, StringComparison.Ordinal) ||
                    !FixedDigest(value.ClientIdDigest, Digest(clientId)))
                    return false;
                correlation = value;
                return true;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(clear);
            }
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            return false;
        }
    }

    private bool TryCorrelation(
        string protectedValue,
        ManagerProviderCallbackContext context,
        string clientId,
        out EntraWorkforceManagerCorrelation correlation) =>
        TryCorrelation(protectedValue,
            new ManagerProviderBeginContext(context.Authority, context.CallbackUri, "/", context.Purpose),
            clientId,
            out correlation);

    private bool TryReadIdentityPayload(
        string token,
        string clientId,
        string organizationId,
        string nonce,
        out string subject,
        out DateTimeOffset authenticatedAt)
    {
        subject = string.Empty;
        authenticatedAt = default;
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3 || parts[1].Length > 24576) return false;
            using var document = JsonDocument.Parse(WebEncoders.Base64UrlDecode(parts[1]));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !ExactAudience(root, clientId) ||
                !String(root, "tid", 36, out var tenantId) ||
                !string.Equals(tenantId, organizationId, StringComparison.Ordinal) ||
                !String(root, "sub", 512, out subject) ||
                !String(root, "nonce", 128, out var tokenNonce) ||
                !string.Equals(tokenNonce, nonce, StringComparison.Ordinal) ||
                !Integer(root, "iat", out var issuedAt))
                return false;

            var now = _time.GetUtcNow().ToUnixTimeSeconds();
            if (issuedAt > now + (long)TokenClockSkew.TotalSeconds || issuedAt < now - 7200)
                return false;
            authenticatedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAt);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or FormatException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool ValidContext(ManagerProviderBeginContext context) =>
        context.Authority.Provider == ManagerIdentityProviders.EntraWorkforce &&
        context.Authority.BindingId > 0 &&
        EntraWorkforceEndpointRules.CanonicalTenant(context.Authority.OrganizationId) &&
        EntraWorkforceEndpointRules.IsCanonicalAuthority(
            context.Authority.Authority, context.Authority.OrganizationId) &&
        string.Equals(context.Authority.Issuer, context.Authority.Authority, StringComparison.Ordinal) &&
        Opaque(context.Purpose, 128) &&
        SafeCallback(context.CallbackUri, CallbackPath);

    private static bool ValidContext(ManagerProviderCallbackContext context) =>
        ValidContext(new ManagerProviderBeginContext(
            context.Authority, context.CallbackUri, "/", context.Purpose));

    private static bool SafeCallback(Uri uri, string callbackPath) =>
        uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment) &&
        string.Equals(uri.AbsolutePath, callbackPath, StringComparison.Ordinal);

    private static bool TryCredential(ReadOnlyMemory<byte> bytes, int max, out string value)
    {
        value = string.Empty;
        if (bytes.Length is 0 || bytes.Length > max) return false;
        try
        {
            value = new UTF8Encoding(false, true).GetString(bytes.Span);
            return Opaque(value, max);
        }
        catch (DecoderFallbackException)
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
        return audience[0].ValueKind == JsonValueKind.String &&
               string.Equals(audience[0].GetString(), clientId, StringComparison.Ordinal);
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

    private static string Digest(string value) =>
        WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool FixedDigest(string left, string right)
    {
        if (left.Length != right.Length) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    }

    private static bool ValidStateHandle(string value) => value is { Length: 43 } &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static bool Opaque(string? value, int max) => value is { Length: > 0 } && value.Length <= max &&
        value == value.Trim() && !value.Any(char.IsControl);

    private static Result<T, AeroError> Fail<T>() =>
        Prelude.Fail<T, AeroError>(AeroError.CreateError("Manager sign-in is unavailable."));
}
