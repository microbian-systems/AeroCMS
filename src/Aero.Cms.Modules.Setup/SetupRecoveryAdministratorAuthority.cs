using Aero.Cms.Abstractions.Authentication;

namespace Aero.Cms.Modules.Setup;

/// <summary>Reads the recovery-administrator authority from the durable singleton setup document.</summary>
public sealed class SetupRecoveryAdministratorAuthority(ISetupStateStore setupStateStore)
    : IRecoveryAdministratorAuthority
{
    /// <inheritdoc />
    public async Task<long?> GetUserIdAsync(CancellationToken cancellationToken = default)
    {
        var state = await setupStateStore.LoadAsync(cancellationToken);
        return state is { IsComplete: true, RecoveryAdministratorUserId: > 0 }
            ? state.RecoveryAdministratorUserId
            : null;
    }
}
