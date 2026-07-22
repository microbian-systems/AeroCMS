using System.Text;
using Aero.Cms.Abstractions.Authentication;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.Extensions.Configuration;

namespace Aero.Cms.Modules.Identity;

public sealed class UnavailableManagerProviderSecretSource : IManagerProviderSecretSource
{
    public Task<Result<ManagerProviderCredentialBundle, AeroError>> ReadAsync(
        ManagerProviderSecretReference reference,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Prelude.Fail<ManagerProviderCredentialBundle, AeroError>(
            AeroError.CreateError("Manager provider credentials are unavailable.")));
}

/// <summary>Development-only configuration seam. It is registered only when both development gates are true.</summary>
public sealed class DevelopmentManagerProviderSecretSource(IConfiguration configuration)
    : IManagerProviderSecretSource
{
    public const string EnabledConfigurationKey =
        "AeroCms:Authentication:ManagerFederation:EnableDevelopmentProviderSecrets";

    public Task<Result<ManagerProviderCredentialBundle, AeroError>> ReadAsync(
        ManagerProviderSecretReference reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ManagerIdentityProviders.IsSupported(reference.Provider) ||
            reference.VaultId <= 0 ||
            !ManagerIdentityAuthorityRules.IsCanonicalVaultEnvironment(reference.VaultEnvironment) ||
            !string.Equals(reference.CredentialPath,
                ManagerProviderSecretReference.CanonicalCredentialPath(reference.Provider),
                StringComparison.Ordinal))
            return Failed();

        var section = configuration.GetSection(
            $"AeroCms:Authentication:ManagerFederation:DevelopmentSecrets:{reference.Provider}");
        var clientId = Bytes(section["ClientId"]);
        var clientSecret = Bytes(section["ClientSecret"]);
        var apiKey = Bytes(section["ApiKey"]);

        var valid = reference.Provider == ManagerIdentityProviders.EntraWorkforce
            ? clientId is not null && clientSecret is not null
            : clientId is not null && apiKey is not null;
        if (!valid)
        {
            Zero(clientId);
            Zero(clientSecret);
            Zero(apiKey);
            return Failed();
        }

        return Task.FromResult(Prelude.Ok<ManagerProviderCredentialBundle, AeroError>(
            new ManagerProviderCredentialBundle(clientId, clientSecret, apiKey)));
    }

    private static byte[]? Bytes(string? value) =>
        value is { Length: > 0 and <= 4096 } && string.Equals(value, value.Trim(), StringComparison.Ordinal)
            ? Encoding.UTF8.GetBytes(value)
            : null;

    private static void Zero(byte[]? value)
    {
        if (value is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(value);
    }

    private static Task<Result<ManagerProviderCredentialBundle, AeroError>> Failed() =>
        Task.FromResult(Prelude.Fail<ManagerProviderCredentialBundle, AeroError>(
            AeroError.CreateError("Manager provider credentials are unavailable.")));
}
