using Marten;
using Aero.Cms.Modules.Setup.Bootstrap;

namespace Aero.Cms.Modules.Setup;

public interface ISetupStateStore
{
    Task<SetupStateDocument?> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed class MartenSetupStateStore(IQuerySession querySession) : ISetupStateStore
{
    public Task<SetupStateDocument?> LoadAsync(CancellationToken cancellationToken = default)
        => querySession.LoadAsync<SetupStateDocument>(SetupStateDocument.FixedId, cancellationToken);
}

public interface ISetupInitializationService
{
    BootstrapState GetBootstrapState();
    bool HasBootstrapConfig();
    Task<SetupStateDocument?> GetStateAsync(CancellationToken cancellationToken = default);
    Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken = default);
}

public sealed class SetupInitializationService(
    IBootstrapStateProvider bootstrapStateProvider) : ISetupInitializationService
{
    public BootstrapState GetBootstrapState() => bootstrapStateProvider.GetState();

    public bool HasBootstrapConfig() => GetBootstrapState().HasBootstrapConfig;

    public Task<SetupStateDocument?> GetStateAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<SetupStateDocument?>(null);

    public Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken = default)
    {
        var bootstrapState = GetBootstrapState();
        return Task.FromResult(bootstrapState.IsRunningMode || bootstrapState.IsConfiguredMode);
    }
}
