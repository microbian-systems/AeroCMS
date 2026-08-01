using System.Security.Cryptography;
using Aero.Core;

namespace Aero.Cms.Abstractions.Authentication;

/// <summary>Canonical external authority identifiers supported by AeroCMS.</summary>
public static class ExternalMemberProviders
{
    public const string WorkOs = "workos";
    public const string EntraExternalId = "entra_external_id";

    public static bool IsSupported(string? value) => value is WorkOs or EntraExternalId;
}

/// <summary>Identifies the exact server-approved Aero.Vault credential bundle for an external authority.</summary>
public sealed record ExternalProviderSecretReference(
    long VaultId,
    string VaultEnvironment,
    long TenantId,
    string Provider,
    string CredentialPath)
{
    public static string CanonicalCredentialPath(long tenantId, string provider) =>
        $"/aerocms/tenants/{tenantId}/external-identity/{provider}/credentials";
}

/// <summary>Read-only view of a credential byte buffer owned by a credential bundle.</summary>
public readonly struct ExternalProviderCredentialLease(ReadOnlyMemory<byte> bytes)
{
    public ReadOnlyMemory<byte> Bytes { get; } = bytes;
}

/// <summary>Owns provider credential bytes and clears every owned buffer on disposal.</summary>
public sealed class ExternalProviderCredentialBundle : IDisposable
{
    private byte[]? _clientId;
    private byte[]? _clientSecret;
    private byte[]? _apiKey;

    public ExternalProviderCredentialBundle(byte[]? clientId, byte[]? clientSecret, byte[]? apiKey)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        _apiKey = apiKey;
    }

    public ExternalProviderCredentialLease LeaseClientId() => new(_clientId ?? ReadOnlyMemory<byte>.Empty);
    public ExternalProviderCredentialLease LeaseClientSecret() => new(_clientSecret ?? ReadOnlyMemory<byte>.Empty);
    public ExternalProviderCredentialLease LeaseApiKey() => new(_apiKey ?? ReadOnlyMemory<byte>.Empty);

    public void Dispose()
    {
        Clear(ref _clientId);
        Clear(ref _clientSecret);
        Clear(ref _apiKey);
    }

    private static void Clear(ref byte[]? buffer)
    {
        var owned = Interlocked.Exchange(ref buffer, null);
        if (owned is not null) CryptographicOperations.ZeroMemory(owned);
    }
}

/// <summary>Reads provider credentials from the separate Aero.Vault trust boundary.</summary>
public interface IExternalProviderSecretSource
{
    Task<Result<ExternalProviderCredentialBundle, AeroError>> ReadAsync(
        ExternalProviderSecretReference reference,
        CancellationToken cancellationToken = default);
}

/// <summary>Persisted external-authority data projected without persistence or HTTP dependencies.</summary>
public sealed record ExternalProviderAuthority(long BindingId, long TenantId, string Provider, string Issuer,
    string OrganizationId, string Authority, ExternalProviderSecretReference SecretReference);

public sealed record ExternalProviderBeginContext(ExternalProviderAuthority Authority, long SiteId,
    Uri CallbackUri, string ReturnPath);

public sealed record ExternalProviderAuthorizationPreparation(string ProtectedProviderCorrelation);

public enum ExternalProviderAuthorizationChallengeKind { Redirect, NamedScheme }

/// <summary>Provider-neutral instruction for the host to initiate external authorization.</summary>
public sealed record ExternalProviderAuthorizationChallenge(ExternalProviderAuthorizationChallengeKind Kind,
    string Target, IReadOnlyDictionary<string, string> Parameters);

public sealed record ExternalProviderCallbackContext(ExternalProviderAuthority Authority, long SiteId, Uri CallbackUri,
    string AuthenticationHandle, string ProtectedProviderCorrelation, string? Code, string? Error,
    string? ClientIp, string? UserAgent);

public sealed record ExternalProviderLogoutContext(ExternalProviderAuthority Authority, long SiteId, Uri ReturnUri,
    string? ProviderSessionReference);

/// <summary>Provider adapters never receive local session/cookie services.</summary>
public interface IExternalMemberProviderStrategy
{
    string Provider { get; }
    Task<Result<ExternalProviderAuthorizationPreparation, AeroError>> PrepareAuthorizationAsync(
        ExternalProviderBeginContext context, ExternalProviderCredentialBundle credentials, CancellationToken cancellationToken = default);
    Task<Result<ExternalProviderAuthorizationChallenge, AeroError>> CreateAuthorizationAsync(
        ExternalProviderBeginContext context, ExternalProviderAuthorizationPreparation preparation,
        string authenticationHandle, ExternalProviderCredentialBundle credentials, CancellationToken cancellationToken = default);
    Task<Result<ValidatedExternalIdentity, AeroError>> AuthenticateAsync(
        ExternalProviderCallbackContext context, ExternalProviderCredentialBundle credentials, CancellationToken cancellationToken = default);
    Task<Result<ExternalProviderAuthorizationChallenge, AeroError>> PrepareLogoutAsync(
        ExternalProviderLogoutContext context, ExternalProviderCredentialBundle credentials, CancellationToken cancellationToken = default);
}
