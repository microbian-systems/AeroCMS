using Marten;

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
    Task<SetupStateDocument?> GetStateAsync(CancellationToken cancellationToken = default);
    Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken = default);
}

public sealed class SetupInitializationService(ISetupStateStore setupStateStore) : ISetupInitializationService
{
    public Task<SetupStateDocument?> GetStateAsync(CancellationToken cancellationToken = default)
        => setupStateStore.LoadAsync(cancellationToken);

    public async Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken = default)
    {
        var state = await setupStateStore.LoadAsync(cancellationToken);
        return state?.IsComplete == true;
    }
}
