using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Service responsible for completing the setup process in the setup app,
/// persisting configuration, and triggering the transition to the main application.
/// </summary>
/// <remarks>
/// This is distinct from <see cref="ISetupCompletionService"/> (seeding) and 
/// <see cref="IRuntimeBootstrapInitializer"/> (runtime initialization).
/// This service handles the setup app shutdown and handoff only.
/// </remarks>
public interface ISetupBootstrapHandoffService
{
    /// <summary>
    /// Completes the setup process, persists configuration, and triggers application shutdown.
    /// </summary>
    /// <param name="request">The seed database request containing all setup configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure with error messages.</returns>
    Task<SetupBootstrapHandoffResult> CompleteAndHandoffAsync(SeedDatabaseRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of the setup bootstrap handoff operation.
/// </summary>
public sealed class SetupBootstrapHandoffResult
{
    public bool Succeeded { get; init; }
    public List<string> Errors { get; init; } = [];

    public static SetupBootstrapHandoffResult Success() => new() { Succeeded = true };
    public static SetupBootstrapHandoffResult Failure(params string[] errors) => new() { Succeeded = false, Errors = errors.ToList() };
}

/// <summary>
/// Implementation of <see cref="ISetupBootstrapHandoffService"/> that persists
/// bootstrap configuration and triggers the application shutdown for main app transition.
/// </summary>
public sealed class SetupBootstrapHandoffService(
    IDatabaseBootstrapService databaseBootstrapService,
    ICacheBootstrapService cacheBootstrapService,
    IBootstrapPendingSetupRequestStore pendingSetupRequestStore,
    IBootstrapCompletionWriter bootstrapCompletionWriter,
    IHostApplicationLifetime hostLifetime,
    ILogger<SetupBootstrapHandoffService> logger) : ISetupBootstrapHandoffService
{
    public async Task<SetupBootstrapHandoffResult> CompleteAndHandoffAsync(SeedDatabaseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            logger.LogInformation("Starting setup bootstrap handoff process...");

            // Step 1: Persist database bootstrap configuration
            logger.LogInformation("Persisting database bootstrap configuration...");
            await databaseBootstrapService.PersistAsync(new DatabaseBootstrapModel(
                request.DatabaseMode,
                null, // Connection string is handled separately in the bootstrap config
                request.SecretProvider,
                null, // InfisicalMachineId
                null  // InfisicalClientSecret
            ), cancellationToken);

            // Step 2: Persist cache bootstrap configuration
            logger.LogInformation("Persisting cache bootstrap configuration...");
            await cacheBootstrapService.PersistAsync(new CacheBootstrapModel(
                request.CacheMode,
                null, // Connection string is handled separately
                request.SecretProvider,
                null,
                null
            ), cancellationToken);

            // Step 3: Save the pending seed request for runtime initialization
            logger.LogInformation("Saving pending seed request for runtime initialization...");
            await pendingSetupRequestStore.SaveAsync(request, cancellationToken);

            // Step 4: Mark bootstrap as Configured (not Running yet - that happens after seeding)
            logger.LogInformation("Marking bootstrap state as Configured...");
            await MarkConfiguredAsync(cancellationToken);

            logger.LogInformation("Setup bootstrap handoff completed successfully. Shutting down setup app to transition to main app...");

            // Step 5: Trigger application shutdown - this will cause WaitForShutdownAsync to return
            // The main app will then start with the new configuration
            hostLifetime.StopApplication();

            return SetupBootstrapHandoffResult.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Setup bootstrap handoff failed");
            return SetupBootstrapHandoffResult.Failure(ex.Message);
        }
    }

    private async Task MarkConfiguredAsync(CancellationToken cancellationToken)
    {
        // Write Configured state to bootstrap - seeding will happen in main app
        await bootstrapCompletionWriter.MarkConfiguredAsync(cancellationToken);
    }
}
