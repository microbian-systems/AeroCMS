using AeroDB.Sable;
using Aero.Cms.Modules.Setup.Bootstrap;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Defines an interface for ISetupStateStore.
/// </summary>
public interface ISetupStateStore
{
        /// <summary>
    /// LoadAsync method.
    /// </summary>
Task<SetupStateDocument?> LoadAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a class for AeroSetupStateStore.
/// </summary>
public sealed class AeroSetupStateStore(IQuerySession querySession) : ISetupStateStore
{
        /// <summary>
    /// LoadAsync method.
    /// </summary>
public Task<SetupStateDocument?> LoadAsync(CancellationToken cancellationToken = default)
        => querySession.LoadAsync<SetupStateDocument>(SetupStateDocument.FixedId, cancellationToken);
}

/// <summary>
/// Defines an interface for ISetupInitializationService.
/// </summary>
public interface ISetupInitializationService
{
        /// <summary>
    /// GetBootstrapState method.
    /// </summary>
BootstrapState GetBootstrapState();
        /// <summary>
    /// HasBootstrapConfig method.
    /// </summary>
bool HasBootstrapConfig();
        /// <summary>
    /// GetStateAsync method.
    /// </summary>
Task<SetupStateDocument?> GetStateAsync(CancellationToken cancellationToken = default);
        /// <summary>
    /// IsSetupCompleteAsync method.
    /// </summary>
Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a class for SetupInitializationService.
/// </summary>
public sealed class SetupInitializationService(
    IBootstrapStateProvider bootstrapStateProvider) : ISetupInitializationService
{
        /// <summary>
    /// GetBootstrapState method.
    /// </summary>
public BootstrapState GetBootstrapState() => bootstrapStateProvider.GetState();

        /// <summary>
    /// HasBootstrapConfig method.
    /// </summary>
public bool HasBootstrapConfig() => GetBootstrapState().HasBootstrapConfig;

        /// <summary>
    /// GetStateAsync method.
    /// </summary>
public Task<SetupStateDocument?> GetStateAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<SetupStateDocument?>(null);

        /// <summary>
    /// IsSetupCompleteAsync method.
    /// </summary>
public Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken = default)
    {
        var bootstrapState = GetBootstrapState();
        return Task.FromResult(bootstrapState.IsRunningMode || bootstrapState.IsConfiguredMode);
    }
}
