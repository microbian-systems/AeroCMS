using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aero.Cms.Abstractions.Authentication;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Aero.Cms.Modules.WorkOS;

/// <summary>Authenticates installation managers with a dedicated WorkOS organization.</summary>
public sealed class WorkOsManagerIdentityProviderStrategy : IManagerIdentityProviderStrategy
{
    internal const string CallbackPath = ManagerFederationRoutes.WorkOsCallbackPath;
    private const string Issuer = "https://api.workos.com";
    private static readonly TimeSpan CorrelationLifetime = TimeSpan.FromMinutes(10);
    private readonly ITimeLimitedDataProtector _protector;
    private readonly WorkOsAuthenticationClient _authentication;
    private readonly TimeProvider _time;

    public WorkOsManagerIdentityProviderStrategy(
        WorkOsAuthenticationClient authentication,
        IDataProtectionProvider protection,
        TimeProvider time)
    {
        _authentication = authentication;
        _protector = protection
            .CreateProtector("AeroCms.ManagerFederation.WorkOs.Correlation.v1")
            .ToTimeLimitedDataProtector();
        _time = time;
    }

    public string Provider => ManagerIdentityProviders.WorkOs;

    public Task<Result<ManagerProviderAuthorizationPreparation, AeroError>> PrepareAuthorizationAsync(
        ManagerProviderBeginContext context,
        ManagerProviderCredentialBundle credentials,
        CancellationToken cancellationToken = default)
    {
        if (!ValidContext(context) ||
            !TryCredential(credentials.LeaseClientId().Bytes, 512, out var clientId))
            return Task.FromResult(Fail<ManagerProviderAuthorizationPreparation>());

        Span<byte> random = stackalloc byte[32];
        RandomNumberGenerator.Fill(random);
        var correlation = new WorkOsManagerCorrelation(
            context.Authority.BindingId,
            Provider,
            context.Authority.Authority,
            context.Authority.OrganizationId,
            context.CallbackUri.AbsoluteUri,
            context.Purpose,
            Digest(clientId),
            WebEncoders.Base64UrlEncode(random));
        var json = JsonSerializer.SerializeToUtf8Bytes(
            correlation, WorkOsManagerJsonContext.Default.WorkOsManagerCorrelation);
        try
        {
            var protectedValue = WebEncoders.Base64UrlEncode(
                _protector.Protect(json, CorrelationLifetime));
            return Task.FromResult(Prelude.Ok<ManagerProviderAuthorizationPreparation, AeroError>(
                new(protectedValue)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
        }
    }

    public Task<Result<ManagerProviderAuthorizationChallenge, AeroError>> CreateAuthorizationAsync(
        ManagerProviderBeginContext context,
        ManagerProviderAuthorizationPreparation preparation,
        string stateHandle,
        ManagerProviderCredentialBundle credentials,
        CancellationToken cancellationToken = default)
    {
        if (!ValidContext(context) || !ValidStateHandle(stateHandle) ||
            !TryCredential(credentials.LeaseClientId().Bytes, 512, out var clientId) ||
            !TryCorrelation(preparation.ProtectedProviderCorrelation, context, clientId, out var correlation))
            return Task.FromResult(Fail<ManagerProviderAuthorizationChallenge>());

        var challenge = WebEncoders.Base64UrlEncode(
            SHA256.HashData(Encoding.ASCII.GetBytes(correlation.Verifier)));
        var values = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["provider"] = "authkit",
            ["client_id"] = clientId,
            ["redirect_uri"] = context.CallbackUri.AbsoluteUri,
            ["organization_id"] = context.Authority.OrganizationId,
            ["state"] = stateHandle,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        };
        var target = QueryHelpers.AddQueryString(
            "https://api.workos.com/user_management/authorize", values!);
        return Task.FromResult(Prelude.Ok<ManagerProviderAuthorizationChallenge, AeroError>(
            new(new Uri(target))));
    }

    public async Task<Result<ValidatedManagerIdentity, AeroError>> AuthenticateAsync(
        ManagerProviderCallbackContext context,
        ManagerProviderCredentialBundle credentials,
        CancellationToken cancellationToken = default)
    {
        if (context.Error is not null || !Opaque(context.Code, 2048) || !ValidContext(context) ||
            !TryCredential(credentials.LeaseClientId().Bytes, 512, out var clientId) ||
            !TryCredential(credentials.LeaseApiKey().Bytes, 2048, out var apiKey) ||
            !TryCorrelation(context.ProtectedProviderCorrelation, context, clientId, out var correlation))
            return Fail<ValidatedManagerIdentity>();

        var response = await _authentication.AuthenticateAsync(
            new WorkOsAuthenticateRequest(
                clientId,
                apiKey,
                "authorization_code",
                context.Code!,
                correlation.Verifier,
                null,
                null),
            cancellationToken);
        if (response is not Result<WorkOsAuthenticateResponse, AeroError>.Ok(var value) ||
            (value.impersonator is { } impersonator &&
             impersonator.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined) ||
            value.user?.id is not { } subject || !Opaque(subject, 512) ||
            !Opaque(value.organization_id, 512) ||
            !string.Equals(value.organization_id, context.Authority.OrganizationId, StringComparison.Ordinal))
            return Fail<ValidatedManagerIdentity>();

        return Prelude.Ok<ValidatedManagerIdentity, AeroError>(new(
            Provider,
            Issuer,
            subject,
            value.organization_id!,
            TrySessionId(value.access_token),
            _time.GetUtcNow()));
    }

    private bool TryCorrelation(
        string protectedValue,
        ManagerProviderBeginContext context,
        string clientId,
        out WorkOsManagerCorrelation correlation)
    {
        correlation = default!;
        try
        {
            var clear = _protector.Unprotect(
                WebEncoders.Base64UrlDecode(protectedValue), out var expiresAt);
            try
            {
                var value = JsonSerializer.Deserialize(
                    clear, WorkOsManagerJsonContext.Default.WorkOsManagerCorrelation);
                if (value is null || expiresAt <= _time.GetUtcNow() || value.Verifier.Length != 43 ||
                    value.BindingId != context.Authority.BindingId ||
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
        out WorkOsManagerCorrelation correlation) =>
        TryCorrelation(
            protectedValue,
            new ManagerProviderBeginContext(context.Authority, context.CallbackUri, "/", context.Purpose),
            clientId,
            out correlation);

    private static bool ValidContext(ManagerProviderBeginContext context) =>
        context.Authority.Provider == ManagerIdentityProviders.WorkOs &&
        context.Authority.BindingId > 0 &&
        string.Equals(context.Authority.Issuer, Issuer, StringComparison.Ordinal) &&
        string.Equals(context.Authority.Authority, Issuer, StringComparison.Ordinal) &&
        Opaque(context.Authority.OrganizationId, 512) &&
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

    // This untrusted access-token payload is never used for identity or authorization.
    private static string? TrySessionId(string? token)
    {
        if (!Opaque(token, 32768)) return null;
        var parts = token!.Split('.');
        if (parts.Length != 3 || parts[1].Length > 4096) return null;
        try
        {
            var payload = JsonSerializer.Deserialize(
                WebEncoders.Base64UrlDecode(parts[1]), WorkOsJsonContext.Default.WorkOsJwtPayload);
            return payload?.sid is { } sessionId && Opaque(sessionId, 512) ? sessionId : null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }
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
