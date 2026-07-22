using Aero.Cms.Abstractions.Authentication;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Identity;

/// <summary>Fails closed when the durable Setup-owned mode resolver is unavailable.</summary>
internal sealed class UnavailableManagerAuthenticationModeResolver
    : IManagerAuthenticationModeResolver
{
    public Task<Result<ManagerAuthenticationModeResolution, AeroError>> ResolveAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Prelude.Fail<ManagerAuthenticationModeResolution, AeroError>(
            AeroError.DatabaseError("Manager authentication mode is unavailable.")));
}
