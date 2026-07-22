using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aero.Cms.Abstractions.Authentication;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Json;

namespace Aero.Cms.Modules.WorkOS;

public sealed class WorkOsAuthenticationClient(HttpClient client)
{
    private const int MaxResponseBytes = 1024 * 1024;
    internal async Task<Result<WorkOsAuthenticateResponse, AeroError>> AuthenticateAsync(WorkOsAuthenticateRequest payload, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "user_management/authenticate")
            {
                Content = JsonContent.Create(payload, WorkOsJsonContext.Default.WorkOsAuthenticateRequest)
            };
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode || response.Content is null ||
                !string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
                response.Content.Headers.ContentLength is > MaxResponseBytes) return Fail();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var bytes = new ArrayBufferWriter<byte>();
            var buffer = new byte[81920];
            int count;
            while ((count = await stream.ReadAsync(buffer, ct)) > 0)
            {
                if (bytes.WrittenCount + count > MaxResponseBytes) return Fail();
                bytes.Write(buffer.AsSpan(0, count));
            }
            var value = JsonSerializer.Deserialize(bytes.WrittenSpan, WorkOsJsonContext.Default.WorkOsAuthenticateResponse);
            return value is null ? Fail() : Prelude.Ok<WorkOsAuthenticateResponse, AeroError>(value);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return Fail(); }
        catch (HttpRequestException) { return Fail(); }
        catch (JsonException) { return Fail(); }
        catch (Exception) { return Fail(); }
    }
    private static Result<WorkOsAuthenticateResponse, AeroError> Fail() => Prelude.Fail<WorkOsAuthenticateResponse, AeroError>(AeroError.CreateError("External sign-in is unavailable."));
}

public sealed class WorkOsExternalMemberProviderStrategy(WorkOsAuthenticationClient authentication, IDataProtectionProvider protection, TimeProvider time) : IExternalMemberProviderStrategy
{
    private const string Issuer = "https://api.workos.com";
    private readonly IDataProtector _protector = protection.CreateProtector("AeroCms.ExternalMember.WorkOs.Correlation.v1");
    public string Provider => ExternalMemberProviders.WorkOs;

    public Task<Result<ExternalProviderAuthorizationPreparation, AeroError>> PrepareAuthorizationAsync(ExternalProviderBeginContext context, ExternalProviderCredentialBundle credentials, CancellationToken cancellationToken = default)
    {
        if (!ValidContext(context)) return Task.FromResult(Fail<ExternalProviderAuthorizationPreparation>());
        Span<byte> random = stackalloc byte[32]; RandomNumberGenerator.Fill(random);
        var verifier = WebEncoders.Base64UrlEncode(random);
        var correlation = new WorkOsCorrelation(context.Authority.BindingId, context.Authority.TenantId, context.SiteId, context.CallbackUri.AbsoluteUri, verifier);
        var json = JsonSerializer.SerializeToUtf8Bytes(correlation, WorkOsJsonContext.Default.WorkOsCorrelation);
        return Task.FromResult(Prelude.Ok<ExternalProviderAuthorizationPreparation, AeroError>(new(WebEncoders.Base64UrlEncode(_protector.Protect(json)))));
    }

    public Task<Result<ExternalProviderAuthorizationChallenge, AeroError>> CreateAuthorizationAsync(ExternalProviderBeginContext context, ExternalProviderAuthorizationPreparation preparation, string authenticationHandle, ExternalProviderCredentialBundle credentials, CancellationToken cancellationToken = default)
    {
        if (!ValidContext(context) || !ValidHandle(authenticationHandle) || !TryCorrelation(preparation.ProtectedProviderCorrelation, context, out var c) || !TryUtf8(credentials.LeaseClientId().Bytes, out var clientId)) return Task.FromResult(Fail<ExternalProviderAuthorizationChallenge>());
        using var sha = SHA256.Create(); var challenge = WebEncoders.Base64UrlEncode(sha.ComputeHash(Encoding.ASCII.GetBytes(c.Verifier)));
        var values = new Dictionary<string, string> { ["response_type"] = "code", ["provider"] = "authkit", ["client_id"] = clientId, ["redirect_uri"] = context.CallbackUri.AbsoluteUri, ["organization_id"] = context.Authority.OrganizationId, ["state"] = authenticationHandle, ["code_challenge"] = challenge, ["code_challenge_method"] = "S256" };
        var target = Query("https://api.workos.com/user_management/authorize", values);
        return Task.FromResult(Prelude.Ok<ExternalProviderAuthorizationChallenge, AeroError>(new(ExternalProviderAuthorizationChallengeKind.Redirect, target, new Dictionary<string, string>())));
    }

    public async Task<Result<ValidatedExternalIdentity, AeroError>> AuthenticateAsync(ExternalProviderCallbackContext context, ExternalProviderCredentialBundle credentials, CancellationToken cancellationToken = default)
    {
        if (context.Error is not null || !ValidCode(context.Code) || !ValidContext(new(context.Authority, context.SiteId, context.CallbackUri, "")) || !TryCorrelation(context.ProtectedProviderCorrelation, new(context.Authority, context.SiteId, context.CallbackUri, ""), out var c) || !TryUtf8(credentials.LeaseClientId().Bytes, out var id) || !TryUtf8(credentials.LeaseApiKey().Bytes, out var key)) return Fail<ValidatedExternalIdentity>();
        var reply = await authentication.AuthenticateAsync(new(id, key, "authorization_code", context.Code!, c.Verifier, Bounded(context.ClientIp, 128), Bounded(context.UserAgent, 512)), cancellationToken);
        if (reply is not Result<WorkOsAuthenticateResponse, AeroError>.Ok(var r) || (r.impersonator is { } impersonator && impersonator.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined) || r.user is not { id: { } subject, email: { } email, email_verified: { } verified } || string.IsNullOrWhiteSpace(r.access_token) || !Opaque(subject, 512) || !ValidEmail(email) || !Opaque(r.organization_id, 512) || !string.Equals(r.organization_id, context.Authority.OrganizationId, StringComparison.Ordinal)) return Fail<ValidatedExternalIdentity>();
        var name = string.Join(' ', new[] { r.user.first_name, r.user.last_name }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        return Prelude.Ok<ValidatedExternalIdentity, AeroError>(new(Provider, Issuer, subject, r.organization_id!, email, verified, name.Length == 0 ? null : name, TrySid(r.access_token), time.GetUtcNow()));
    }

    public Task<Result<ExternalProviderAuthorizationChallenge, AeroError>> PrepareLogoutAsync(ExternalProviderLogoutContext context, ExternalProviderCredentialBundle credentials, CancellationToken cancellationToken = default)
    {
        if (!ValidContext(new(context.Authority, context.SiteId, context.ReturnUri, "")) || !Opaque(context.ProviderSessionReference, 512)) return Task.FromResult(Fail<ExternalProviderAuthorizationChallenge>());
        var target = Query("https://api.workos.com/user_management/sessions/logout", new Dictionary<string, string> { ["session_id"] = context.ProviderSessionReference!, ["return_to"] = context.ReturnUri.AbsoluteUri });
        return Task.FromResult(Prelude.Ok<ExternalProviderAuthorizationChallenge, AeroError>(new(ExternalProviderAuthorizationChallengeKind.Redirect, target, new Dictionary<string, string>())));
    }

    private bool TryCorrelation(string protectedValue, ExternalProviderBeginContext context, out WorkOsCorrelation correlation)
    { correlation = default!; try { var c = JsonSerializer.Deserialize(_protector.Unprotect(WebEncoders.Base64UrlDecode(protectedValue)), WorkOsJsonContext.Default.WorkOsCorrelation); if (c is null || c.Verifier.Length != 43 || !ValidContext(context) || c.BindingId != context.Authority.BindingId || c.TenantId != context.Authority.TenantId || c.SiteId != context.SiteId || !string.Equals(c.CallbackUri, context.CallbackUri.AbsoluteUri, StringComparison.Ordinal)) return false; correlation = c; return true; } catch (Exception e) when (e is CryptographicException or FormatException or JsonException) { return false; } }
    private static bool ValidContext(ExternalProviderBeginContext c) => c.Authority.Provider == ExternalMemberProviders.WorkOs && c.Authority.Issuer == Issuer && c.Authority.Authority == Issuer && c.Authority.BindingId > 0 && c.Authority.TenantId > 0 && c.SiteId > 0 && SafeHttps(c.CallbackUri) && Opaque(c.Authority.OrganizationId, 512);
    private static bool SafeHttps(Uri u) => u.IsAbsoluteUri && u.Scheme == Uri.UriSchemeHttps && u.IsDefaultPort && string.IsNullOrEmpty(u.UserInfo) && string.IsNullOrEmpty(u.Query) && string.IsNullOrEmpty(u.Fragment);
    private static bool TryUtf8(ReadOnlyMemory<byte> bytes, out string value) { value = string.Empty; if (bytes.Length is 0 or > 512) return false; try { value = new UTF8Encoding(false, true).GetString(bytes.Span); return Opaque(value, 512); } catch (DecoderFallbackException) { return false; } }
    // The access token is never trusted for identity. Its optional sid is retained solely as an opaque upstream logout reference.
    private static string? TrySid(string token) { var p = token.Split('.'); if (p.Length != 3 || p[1].Length > 4096) return null; try { var payload = JsonSerializer.Deserialize(WebEncoders.Base64UrlDecode(p[1]), WorkOsJsonContext.Default.WorkOsJwtPayload); return payload?.sid is { } v && Opaque(v, 512) ? v : null; } catch (Exception e) when (e is FormatException or JsonException) { return null; } }
    private static string Query(string baseUri, IReadOnlyDictionary<string, string> values) => Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(baseUri, values!);
    private static bool ValidHandle(string x) => x is { Length: >= 46 and <= 64 } && x.Count(c => c == '.') == 1 && !x.Any(char.IsControl);
    private static bool ValidCode(string? x) => Opaque(x, 2048);
    private static string? Bounded(string? x, int n) => x is { Length: > 0 } && x.Length <= n && !x.Any(char.IsControl) ? x : null;
    private static bool Opaque(string? x, int n) => x is { Length: > 0 } && x.Length <= n && x == x.Trim() && !x.Any(char.IsControl);
    private static bool ValidEmail(string x) => x.Length <= 320 && x.Contains('@') && !x.Any(char.IsControl);
    private static Result<T, AeroError> Fail<T>() => Prelude.Fail<T, AeroError>(AeroError.CreateError("External sign-in is unavailable."));
}
