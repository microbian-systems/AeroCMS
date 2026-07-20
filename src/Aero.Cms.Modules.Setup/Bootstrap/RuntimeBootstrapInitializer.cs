using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Resumes a configured bootstrap workflow after the runtime host starts.
/// </summary>
public interface IRuntimeBootstrapInitializer
{
    /// <summary>
    /// Completes pending setup work when the persisted state is configured.
    /// </summary>
    /// <param name="cancellationToken">Cancels pending-request loading, setup completion, or cleanup.</param>
    /// <exception cref="InvalidOperationException">Setup completion returns one or more failures.</exception>
Task InitializeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Executes deferred setup completion from the protected handoff payload.
/// </summary>
/// <remarks>
/// Initialization is a no-op outside the configured state. A missing payload is logged and
/// left recoverable; a failed completion is promoted to an exception and the payload is retained.
/// Successful completion clears the payload.
/// </remarks>
public sealed class RuntimeBootstrapInitializer(
    ISetupInitializationService setupInitializationService,
    IBootstrapPendingSetupRequestStore pendingSetupRequestStore,
    ISetupCompletionService setupCompletionService,
    ILogger<RuntimeBootstrapInitializer> logger) : IRuntimeBootstrapInitializer
{
    /// <inheritdoc />
public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var bootstrap = setupInitializationService.GetBootstrapState();
        if (!bootstrap.IsConfiguredMode)
        {
            return;
        }

        var request = await pendingSetupRequestStore.LoadAsync(cancellationToken);
        if (request == null)
        {
            logger.LogWarning("Bootstrap state is Configured but no pending seed payload exists.");
            return;
        }

        var result = await setupCompletionService.CompleteAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Runtime bootstrap initialization failed: {string.Join("; ", result.Errors)}");
        }

        await pendingSetupRequestStore.ClearAsync(cancellationToken);
        logger.LogInformation("Runtime bootstrap initialization completed successfully.");
    }
}
