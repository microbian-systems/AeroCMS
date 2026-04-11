using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Setup.Bootstrap;

public interface IRuntimeBootstrapInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed class RuntimeBootstrapInitializer(
    ISetupInitializationService setupInitializationService,
    IBootstrapPendingSetupRequestStore pendingSetupRequestStore,
    ISetupCompletionService setupCompletionService,
    ILogger<RuntimeBootstrapInitializer> logger) : IRuntimeBootstrapInitializer
{
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
