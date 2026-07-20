using AeroDB.Sable;
using Aero.Cms.Modules.Setup.Bootstrap;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Loads the singleton durable setup-state document.
/// </summary>
public interface ISetupStateStore
{
    /// <summary>
    /// Loads the installation outcome from the current data store.
    /// </summary>
    /// <param name="cancellationToken">Cancels the database query.</param>
    /// <returns>The setup-state document, or <see langword="null"/> when it has not been created.</returns>
Task<SetupStateDocument?> LoadAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Loads setup state from an AeroDB Sable query session.
/// </summary>
public sealed class AeroSetupStateStore(IQuerySession querySession) : ISetupStateStore
{
    /// <inheritdoc />
public Task<SetupStateDocument?> LoadAsync(CancellationToken cancellationToken = default)
        => querySession.LoadAsync<SetupStateDocument>(SetupStateDocument.FixedId, cancellationToken);
}

/// <summary>
/// Exposes the bootstrap state used to gate startup and requests.
/// </summary>
public interface ISetupInitializationService
{
    /// <summary>
    /// Gets a snapshot of the file-based bootstrap lifecycle.
    /// </summary>
    /// <returns>The current bootstrap state.</returns>
BootstrapState GetBootstrapState();
    /// <summary>
    /// Determines whether bootstrap configuration has been persisted.
    /// </summary>
    /// <returns><see langword="true"/> when bootstrap configuration is present.</returns>
bool HasBootstrapConfig();
    /// <summary>
    /// Gets durable setup state when supported by the current initialization implementation.
    /// </summary>
    /// <param name="cancellationToken">Cancels state retrieval.</param>
    /// <returns>The setup-state document, or <see langword="null"/> when unavailable.</returns>
Task<SetupStateDocument?> GetStateAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Determines whether the setup gate may allow normal application requests.
    /// </summary>
    /// <param name="cancellationToken">Cancels state retrieval when the implementation performs I/O.</param>
    /// <returns><see langword="true"/> when bootstrap is configured or running.</returns>
Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Derives startup and request-gating decisions from the file-based bootstrap state.
/// </summary>
/// <remarks>
/// The current implementation does not load the durable setup document and returns
/// <see langword="null"/> from <see cref="GetStateAsync"/>. Configured is treated as
/// complete for request gating so the runtime host can perform deferred seeding.
/// </remarks>
public sealed class SetupInitializationService(
    IBootstrapStateProvider bootstrapStateProvider) : ISetupInitializationService
{
    /// <inheritdoc />
public BootstrapState GetBootstrapState() => bootstrapStateProvider.GetState();

    /// <inheritdoc />
public bool HasBootstrapConfig() => GetBootstrapState().HasBootstrapConfig;

    /// <inheritdoc />
public Task<SetupStateDocument?> GetStateAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<SetupStateDocument?>(null);

    /// <inheritdoc />
public Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken = default)
    {
        var bootstrapState = GetBootstrapState();
        return Task.FromResult(bootstrapState.IsRunningMode || bootstrapState.IsConfiguredMode);
    }
}
