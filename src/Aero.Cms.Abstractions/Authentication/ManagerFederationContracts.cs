using System.Security.Cryptography;
using Aero.Core;

namespace Aero.Cms.Abstractions.Authentication;

/// <summary>Canonical providers supported for installation-wide CMS manager federation.</summary>
public static class ManagerIdentityProviders
{
    public const string EntraWorkforce = "entra_workforce";
    public const string WorkOs = "workos";

    public static bool IsSupported(string? provider) => provider is EntraWorkforce or WorkOs;
}

/// <summary>Stable runtime states for the requested CMS manager authentication provider.</summary>
public static class ManagerAuthenticationModeStatuses
{
    /// <summary>Local Identity is both requested and effective.</summary>
    public const string Local = "local";

    /// <summary>A remote provider is requested, but local Identity remains effective until activation.</summary>
    public const string Pending = "pending";

    /// <summary>The exact requested remote authority is verified, active, and effective.</summary>
    public const string Remote = "remote";
}

/// <summary>Freshly resolved installation-wide manager authentication mode.</summary>
public sealed record ManagerAuthenticationModeResolution(
    string RequestedProvider,
    string EffectiveProvider,
    string Status,
    long? AuthorityBindingId);

/// <summary>
/// Resolves requested manager authentication intent against durable authority activation evidence.
/// </summary>
public interface IManagerAuthenticationModeResolver
{
    /// <summary>Loads a fresh persistence snapshot and derives the effective provider.</summary>
    Task<Result<ManagerAuthenticationModeResolution, AeroError>> ResolveAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>Canonical callback paths shared by manager-federation routes and provider adapters.</summary>
public static class ManagerFederationRoutes
{
    public const string EntraWorkforceCallbackPath = "/api/v1/admin/auth/callback/entra-workforce";
    public const string WorkOsCallbackPath = "/api/v1/admin/auth/callback/workos";
}

/// <summary>Identifies the server-approved Aero.Vault credential bundle for manager federation.</summary>
public sealed record ManagerProviderSecretReference(
    long VaultId,
    string VaultEnvironment,
    string Provider,
    string CredentialPath)
{
    public static string CanonicalCredentialPath(string provider) =>
        $"/aerocms/manager-identity/{provider}/credentials";
}

public readonly struct ManagerProviderCredentialLease(ReadOnlyMemory<byte> bytes)
{
    public ReadOnlyMemory<byte> Bytes { get; } = bytes;
}

/// <summary>Owns manager-provider credential buffers and zeroes them on disposal.</summary>
public sealed class ManagerProviderCredentialBundle : IDisposable
{
    private byte[]? _clientId;
    private byte[]? _clientSecret;
    private byte[]? _apiKey;

    public ManagerProviderCredentialBundle(byte[]? clientId, byte[]? clientSecret, byte[]? apiKey)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        _apiKey = apiKey;
    }

    public ManagerProviderCredentialLease LeaseClientId() => new(_clientId ?? ReadOnlyMemory<byte>.Empty);
    public ManagerProviderCredentialLease LeaseClientSecret() => new(_clientSecret ?? ReadOnlyMemory<byte>.Empty);
    public ManagerProviderCredentialLease LeaseApiKey() => new(_apiKey ?? ReadOnlyMemory<byte>.Empty);

    public void Dispose()
    {
        Clear(ref _clientId);
        Clear(ref _clientSecret);
        Clear(ref _apiKey);
    }

    private static void Clear(ref byte[]? value)
    {
        var owned = Interlocked.Exchange(ref value, null);
        if (owned is not null) CryptographicOperations.ZeroMemory(owned);
    }
}

public interface IManagerProviderSecretSource
{
    Task<Result<ManagerProviderCredentialBundle, AeroError>> ReadAsync(
        ManagerProviderSecretReference reference,
        CancellationToken cancellationToken = default);
}

public sealed record ManagerProviderAuthority(
    long BindingId,
    string Provider,
    string Issuer,
    string OrganizationId,
    string Authority,
    string PublicOrigin,
    ManagerProviderSecretReference SecretReference);

public sealed record ManagerProviderBeginContext(
    ManagerProviderAuthority Authority,
    Uri CallbackUri,
    string ReturnPath,
    string Purpose);

public sealed record ManagerProviderAuthorizationPreparation(string ProtectedProviderCorrelation);

public sealed record ManagerProviderAuthorizationChallenge(Uri RedirectUri);

public sealed record ManagerProviderCallbackContext(
    ManagerProviderAuthority Authority,
    Uri CallbackUri,
    string StateHandle,
    string ProtectedProviderCorrelation,
    string? Code,
    string? Error,
    string Purpose);

public sealed record ValidatedManagerIdentity(
    string Provider,
    string Issuer,
    string Subject,
    string OrganizationId,
    string? ProviderSessionReference,
    DateTimeOffset AuthenticatedAt);

/// <summary>Provider adapters have no access to Identity, local cookies, or manager sessions.</summary>
public interface IManagerIdentityProviderStrategy
{
    string Provider { get; }

    Task<Result<ManagerProviderAuthorizationPreparation, AeroError>> PrepareAuthorizationAsync(
        ManagerProviderBeginContext context,
        ManagerProviderCredentialBundle credentials,
        CancellationToken cancellationToken = default);

    Task<Result<ManagerProviderAuthorizationChallenge, AeroError>> CreateAuthorizationAsync(
        ManagerProviderBeginContext context,
        ManagerProviderAuthorizationPreparation preparation,
        string stateHandle,
        ManagerProviderCredentialBundle credentials,
        CancellationToken cancellationToken = default);

    Task<Result<ValidatedManagerIdentity, AeroError>> AuthenticateAsync(
        ManagerProviderCallbackContext context,
        ManagerProviderCredentialBundle credentials,
        CancellationToken cancellationToken = default);
}

public interface IManagerIdentityProviderStrategyFactory
{
    Result<IManagerIdentityProviderStrategy, AeroError> Resolve(string provider);
}

public sealed record ConfigureManagerIdentityAuthorityRequest(
    string Provider,
    string OrganizationId,
    string Authority,
    string PublicOrigin,
    long VaultId,
    string VaultEnvironment);

public sealed record ManagerIdentityAuthorityResult(
    long BindingId,
    string Provider,
    string Issuer,
    string OrganizationId,
    string Authority,
    string PublicOrigin,
    long VaultId,
    string VaultEnvironment,
    bool IsVerified,
    bool IsActive);

/// <summary>Non-secret installation manager-authentication status.</summary>
public sealed record ManagerAuthenticationStatus(
    string SelectedProvider,
    bool IsFederated,
    bool IsConfigured,
    bool IsVerified,
    bool IsActive,
    string? LoginPath);

/// <summary>Safe manager-authority state returned to administrators.</summary>
public sealed record ManagerIdentityAuthorityState(
    bool Configured,
    string SelectedProvider,
    string? Provider,
    string? Issuer,
    string? OrganizationId,
    string? Authority,
    string? PublicOrigin,
    long? VaultId,
    string? VaultEnvironment,
    bool IsVerified,
    bool IsActive);

public sealed record BeginManagerFederationLinkRequest(
    long RecoveryAdministratorUserId,
    Uri CallbackUri,
    string ReturnPath);

public sealed record BeginManagerFederatedSignInRequest(Uri CallbackUri, string ReturnPath);

public sealed record ManagerFederationBeginResult(string StateHandle, ManagerProviderAuthorizationChallenge Challenge);

public sealed record CompleteManagerFederationCallbackRequest(
    Uri CallbackUri,
    string StateHandle,
    string? Code,
    string? Error);

public sealed record ManagerFederationCallbackResult(
    long UserId,
    long SessionId,
    string LoginProvider,
    string ProviderKey,
    string Provider,
    DateTimeOffset ExpiresAt,
    string ReturnPath,
    bool AuthorityActivated);

/// <summary>Claims placed only in an ordinary manager application cookie.</summary>
public static class ManagerFederationClaims
{
    public const string SessionId = "AeroCms.ManagerFederatedSessionId";
    public const string Provider = "AeroCms.ManagerFederationProvider";
}
