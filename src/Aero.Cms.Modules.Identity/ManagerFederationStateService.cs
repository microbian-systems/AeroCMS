using System.Security.Cryptography;
using System.Text;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.WebUtilities;

namespace Aero.Cms.Modules.Identity;

internal sealed record PreparedManagerFederationCallback(
    ManagerIdentityAuthorityBinding Binding,
    ManagerAuthenticationState State,
    ManagerFederationLinkIntent? LinkIntent);

internal interface IManagerFederationStateService
{
    Task<Result<string, AeroError>> CreateLinkStateAsync(
        ManagerIdentityAuthorityBinding binding,
        long recoveryAdministratorUserId,
        Uri callbackUri,
        string returnPath,
        string protectedProviderCorrelation,
        CancellationToken cancellationToken = default);

    Task<Result<PreparedManagerFederationCallback, AeroError>> PrepareLinkCallbackAsync(
        string stateHandle,
        Uri callbackUri,
        CancellationToken cancellationToken = default);

    Task<Result<string, AeroError>> CreateSignInStateAsync(
        ManagerIdentityAuthorityBinding binding,
        Uri callbackUri,
        string returnPath,
        string protectedProviderCorrelation,
        CancellationToken cancellationToken = default);

    Task<Result<PreparedManagerFederationCallback, AeroError>> PrepareSignInCallbackAsync(
        string stateHandle,
        Uri callbackUri,
        CancellationToken cancellationToken = default);

    Task<Result<PreparedManagerFederationCallback, AeroError>> PrepareCallbackAsync(
        string stateHandle,
        Uri callbackUri,
        string expectedProvider,
        CancellationToken cancellationToken = default);
}

internal sealed class ManagerFederationStateService(
    IDocumentSession session,
    TimeProvider timeProvider) : IManagerFederationStateService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public async Task<Result<string, AeroError>> CreateLinkStateAsync(
        ManagerIdentityAuthorityBinding binding,
        long recoveryAdministratorUserId,
        Uri callbackUri,
        string returnPath,
        string protectedProviderCorrelation,
        CancellationToken cancellationToken = default)
    {
        if (!ManagerIdentityAuthorityProjector.TryProject(binding, requireActive: false, out _) ||
            binding.IsActive || binding.IsVerified || recoveryAdministratorUserId <= 0 ||
            !ManagerFederationValidation.IsExactCallback(callbackUri) ||
            !ManagerFederationValidation.IsSafeReturnPath(returnPath) ||
            string.IsNullOrWhiteSpace(protectedProviderCorrelation))
            return Fail<string>();

        var secret = RandomNumberGenerator.GetBytes(32);
        try
        {
            var now = timeProvider.GetUtcNow();
            var handle = WebEncoders.Base64UrlEncode(secret);
            var linkIntent = new ManagerFederationLinkIntent
            {
                Id = Snowflake.NewId(),
                AuthorityBindingId = binding.Id,
                RecoveryAdministratorUserId = recoveryAdministratorUserId,
                Provider = binding.Provider,
                SecretDigest = Digest("manager-link-intent-v1", secret),
                CallbackUri = callbackUri.AbsoluteUri,
                ExpiresAt = now.Add(Lifetime),
                CreatedOn = now
            };
            var state = new ManagerAuthenticationState
            {
                Id = Snowflake.NewId(),
                AuthorityBindingId = binding.Id,
                LinkIntentId = linkIntent.Id,
                Provider = binding.Provider,
                Purpose = ManagerAuthenticationState.LinkRecoveryAdministratorPurpose,
                SecretDigest = Digest("manager-auth-state-v1", secret),
                CallbackUri = callbackUri.AbsoluteUri,
                ReturnPath = returnPath,
                ProtectedProviderCorrelation = protectedProviderCorrelation,
                ExpiresAt = now.Add(Lifetime),
                CreatedOn = now
            };
            session.Store(linkIntent);
            session.Store(state);
            await session.SaveChangesAsync(cancellationToken);
            return Prelude.Ok<string, AeroError>(handle);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            session.ClearChanges();
            return Fail<string>();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public Task<Result<PreparedManagerFederationCallback, AeroError>> PrepareLinkCallbackAsync(
        string stateHandle,
        Uri callbackUri,
        CancellationToken cancellationToken = default) =>
        PrepareCallbackCoreAsync(stateHandle, callbackUri,
            ManagerAuthenticationState.LinkRecoveryAdministratorPurpose, null, cancellationToken);

    public async Task<Result<string, AeroError>> CreateSignInStateAsync(
        ManagerIdentityAuthorityBinding binding,
        Uri callbackUri,
        string returnPath,
        string protectedProviderCorrelation,
        CancellationToken cancellationToken = default)
    {
        if (!ManagerIdentityAuthorityProjector.TryProject(binding, requireActive: true, out _) ||
            !ManagerFederationValidation.IsExactCallback(callbackUri) ||
            !ManagerFederationValidation.IsSafeReturnPath(returnPath) ||
            string.IsNullOrWhiteSpace(protectedProviderCorrelation))
            return Fail<string>();

        var secret = RandomNumberGenerator.GetBytes(32);
        try
        {
            var now = timeProvider.GetUtcNow();
            var state = new ManagerAuthenticationState
            {
                Id = Snowflake.NewId(), AuthorityBindingId = binding.Id, Provider = binding.Provider,
                Purpose = ManagerAuthenticationState.SignInPurpose,
                SecretDigest = Digest("manager-auth-state-v1", secret),
                CallbackUri = callbackUri.AbsoluteUri, ReturnPath = returnPath,
                ProtectedProviderCorrelation = protectedProviderCorrelation,
                ExpiresAt = now.Add(Lifetime), CreatedOn = now
            };
            session.Store(state);
            await session.SaveChangesAsync(cancellationToken);
            return Prelude.Ok<string, AeroError>(WebEncoders.Base64UrlEncode(secret));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { session.ClearChanges(); return Fail<string>(); }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    public Task<Result<PreparedManagerFederationCallback, AeroError>> PrepareSignInCallbackAsync(
        string stateHandle,
        Uri callbackUri,
        CancellationToken cancellationToken = default) =>
        PrepareCallbackCoreAsync(stateHandle, callbackUri, ManagerAuthenticationState.SignInPurpose,
            null, cancellationToken);

    public Task<Result<PreparedManagerFederationCallback, AeroError>> PrepareCallbackAsync(
        string stateHandle,
        Uri callbackUri,
        string expectedProvider,
        CancellationToken cancellationToken = default) =>
        PrepareCallbackCoreAsync(stateHandle, callbackUri, null, expectedProvider, cancellationToken);

    private async Task<Result<PreparedManagerFederationCallback, AeroError>> PrepareCallbackCoreAsync(
        string stateHandle,
        Uri callbackUri,
        string? expectedPurpose,
        string? expectedProvider,
        CancellationToken cancellationToken)
    {
        if (!ManagerFederationValidation.TryDecodeHandle(stateHandle, out var secret) ||
            !ManagerFederationValidation.IsExactCallback(callbackUri))
            return Fail<PreparedManagerFederationCallback>();

        try
        {
            var stateDigest = Digest("manager-auth-state-v1", secret);
            var state = await session.Query<ManagerAuthenticationState>()
                .FirstOrDefaultAsync(value => value.SecretDigest == stateDigest, cancellationToken);
            if (state is null || state.ConsumedAt is not null || state.ExpiresAt <= timeProvider.GetUtcNow() ||
                state.Purpose is not (ManagerAuthenticationState.LinkRecoveryAdministratorPurpose or
                    ManagerAuthenticationState.SignInPurpose) ||
                (expectedPurpose is not null &&
                    !string.Equals(state.Purpose, expectedPurpose, StringComparison.Ordinal)) ||
                (expectedProvider is not null &&
                    !string.Equals(state.Provider, expectedProvider, StringComparison.Ordinal)) ||
                !string.Equals(state.CallbackUri, callbackUri.AbsoluteUri, StringComparison.Ordinal) ||
                !ManagerIdentityProviders.IsSupported(state.Provider))
                return Fail<PreparedManagerFederationCallback>();

            ManagerFederationLinkIntent? linkIntent = null;
            var isLink = state.Purpose == ManagerAuthenticationState.LinkRecoveryAdministratorPurpose;
            if (isLink)
            {
                if (state.LinkIntentId is not > 0) return Fail<PreparedManagerFederationCallback>();
                linkIntent = await session.LoadAsync<ManagerFederationLinkIntent>(state.LinkIntentId.Value, cancellationToken);
            }
            else if (state.LinkIntentId is not null)
            {
                return Fail<PreparedManagerFederationCallback>();
            }
            var binding = await session.LoadAsync<ManagerIdentityAuthorityBinding>(state.AuthorityBindingId, cancellationToken);
            if (binding is null ||
                (isLink && (linkIntent is null || linkIntent.ConsumedAt is not null ||
                    linkIntent.ExpiresAt <= timeProvider.GetUtcNow() ||
                    !CryptographicOperations.FixedTimeEquals(
                        Encoding.ASCII.GetBytes(linkIntent.SecretDigest),
                        Encoding.ASCII.GetBytes(Digest("manager-link-intent-v1", secret))) ||
                    linkIntent.AuthorityBindingId != state.AuthorityBindingId ||
                    !string.Equals(linkIntent.Provider, state.Provider, StringComparison.Ordinal) ||
                    !string.Equals(linkIntent.CallbackUri, callbackUri.AbsoluteUri, StringComparison.Ordinal))) ||
                !ManagerIdentityAuthorityProjector.TryProject(binding, requireActive: !isLink, out _) ||
                (isLink && (binding.IsActive || binding.IsVerified)) ||
                !string.Equals(binding.Provider, state.Provider, StringComparison.Ordinal))
                return Fail<PreparedManagerFederationCallback>();

            return Prelude.Ok<PreparedManagerFederationCallback, AeroError>(new(binding, state, linkIntent));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Fail<PreparedManagerFederationCallback>();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static string Digest(string purpose, ReadOnlySpan<byte> secret)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.ASCII.GetBytes(purpose));
        hash.AppendData([0]);
        hash.AppendData(secret);
        return WebEncoders.Base64UrlEncode(hash.GetHashAndReset());
    }

    private static Result<T, AeroError> Fail<T>() =>
        Prelude.Fail<T, AeroError>(AeroError.ValidationError(["Manager federation state is invalid."]));
}

internal static class ManagerFederationValidation
{
    public static bool IsExactCallback(Uri? callbackUri) =>
        callbackUri is { IsAbsoluteUri: true, Scheme: "https", IsDefaultPort: true } &&
        string.IsNullOrEmpty(callbackUri.UserInfo) &&
        string.IsNullOrEmpty(callbackUri.Fragment) &&
        string.IsNullOrEmpty(callbackUri.Query);

    public static bool IsSafeReturnPath(string? value) =>
        value is { Length: > 0 and <= 1024 } && value[0] == '/' &&
        !value.StartsWith("//", StringComparison.Ordinal) &&
        !value.Contains('\r') && !value.Contains('\n');

    public static bool TryDecodeHandle(string? handle, out byte[] secret)
    {
        secret = [];
        if (handle is not { Length: 43 }) return false;
        try
        {
            secret = WebEncoders.Base64UrlDecode(handle);
            return secret.Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public static class ManagerIdentityAuthorityProjector
{
    public static bool TryProject(
        ManagerIdentityAuthorityBinding binding,
        bool requireActive,
        out ManagerProviderAuthority authority)
    {
        authority = null!;
        if (binding.Id <= 0 || !ManagerIdentityProviders.IsSupported(binding.Provider) ||
            !string.Equals(binding.SingletonKey,
                ManagerIdentityAuthorityBinding.InstallationSingletonKey, StringComparison.Ordinal) ||
            !ManagerIdentityAuthorityRules.IsCanonicalOrganization(binding.Provider, binding.OrganizationId) ||
            !ManagerIdentityAuthorityRules.IsCanonicalAuthority(binding.Provider, binding.OrganizationId, binding.Authority) ||
            !ManagerIdentityAuthorityRules.IsCanonicalPublicOrigin(binding.PublicOrigin) ||
            !string.Equals(binding.Issuer,
                ManagerIdentityAuthorityRules.CanonicalIssuer(binding.Provider, binding.OrganizationId),
                StringComparison.Ordinal) ||
            !string.Equals(binding.BindingKey,
                ManagerIdentityAuthorityService.Key(binding.Provider, binding.Issuer, binding.OrganizationId),
                StringComparison.Ordinal) ||
            binding.VaultId <= 0 ||
            !ManagerIdentityAuthorityRules.IsCanonicalVaultEnvironment(binding.VaultEnvironment) ||
            !string.Equals(binding.CredentialPath,
                ManagerProviderSecretReference.CanonicalCredentialPath(binding.Provider),
                StringComparison.Ordinal) ||
            (requireActive && (!binding.IsActive || !binding.IsVerified)))
            return false;

        authority = new ManagerProviderAuthority(binding.Id, binding.Provider, binding.Issuer,
            binding.OrganizationId, binding.Authority, binding.PublicOrigin,
            new ManagerProviderSecretReference(binding.VaultId, binding.VaultEnvironment,
                binding.Provider, binding.CredentialPath));
        return true;
    }
}
