using System.Text;
using System.Security.Cryptography;
using Aero.Cms.Abstractions.Authentication;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.Extensions.Configuration;

namespace Aero.Cms.Modules.Identity;

/// <summary>Explicitly opted-in development-only provider credentials from user secrets.</summary>
public sealed class DevelopmentExternalProviderSecretSource(IConfiguration configuration)
    : IExternalProviderSecretSource
{
    public const string EnabledConfigurationKey =
        "AeroCms:Authentication:ExternalMembers:EnableDevelopmentProviderSecrets";
    private const string SecretsPrefix =
        "AeroCms:Authentication:ExternalMembers:DevelopmentSecrets";

    public Task<Result<ExternalProviderCredentialBundle, AeroError>> ReadAsync(
        ExternalProviderSecretReference reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (reference.VaultId <= 0 || reference.TenantId <= 0 ||
            !ExternalMemberProviders.IsSupported(reference.Provider) ||
            !ExternalIdentityAuthorityRules.IsCanonicalVaultEnvironment(reference.VaultEnvironment) ||
            !string.Equals(reference.CredentialPath,
                ExternalProviderSecretReference.CanonicalCredentialPath(reference.TenantId, reference.Provider),
                StringComparison.Ordinal))
            return Unavailable();

        var clientId = configuration[$"{SecretsPrefix}:{reference.Provider}:ClientId"];
        var secondKey = reference.Provider == ExternalMemberProviders.WorkOs ? "ApiKey" : "ClientSecret";
        var secondSecret = configuration[$"{SecretsPrefix}:{reference.Provider}:{secondKey}"];
        if (!IsExactCredential(clientId) || !IsExactCredential(secondSecret))
            return Unavailable();

        byte[]? clientBytes = null;
        byte[]? secondBytes = null;
        try
        {
            clientBytes = Encoding.UTF8.GetBytes(clientId!);
            secondBytes = Encoding.UTF8.GetBytes(secondSecret!);
            var bundle = reference.Provider == ExternalMemberProviders.WorkOs
                ? new ExternalProviderCredentialBundle(clientBytes, null, secondBytes)
                : new ExternalProviderCredentialBundle(clientBytes, secondBytes, null);
            clientBytes = null;
            secondBytes = null;
            return Task.FromResult(Prelude.Ok<ExternalProviderCredentialBundle, AeroError>(bundle));
        }
        catch
        {
            if (clientBytes is not null) CryptographicOperations.ZeroMemory(clientBytes);
            if (secondBytes is not null) CryptographicOperations.ZeroMemory(secondBytes);
            return Unavailable();
        }
    }

    private static bool IsExactCredential(string? value) =>
        value is { Length: > 0 and <= 4096 } &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        Encoding.UTF8.GetByteCount(value) <= 4096;

    private static Task<Result<ExternalProviderCredentialBundle, AeroError>> Unavailable() =>
        Task.FromResult(Prelude.Fail<ExternalProviderCredentialBundle, AeroError>(
            AeroError.CreateError("External provider credentials are unavailable.")));
}
