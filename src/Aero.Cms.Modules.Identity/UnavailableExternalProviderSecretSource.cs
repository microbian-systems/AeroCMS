using Aero.Cms.Abstractions.Authentication;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Identity;

/// <summary>Fail-closed default until a typed client can reach a deployed Aero.Vault Host.</summary>
public sealed class UnavailableExternalProviderSecretSource : IExternalProviderSecretSource
{
    public Task<Result<ExternalProviderCredentialBundle, AeroError>> ReadAsync(
        ExternalProviderSecretReference reference,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Prelude.Fail<ExternalProviderCredentialBundle, AeroError>(
            AeroError.CreateError("External provider credentials are unavailable.")));
}
