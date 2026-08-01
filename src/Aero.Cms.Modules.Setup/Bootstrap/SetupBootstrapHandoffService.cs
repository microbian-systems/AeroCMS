using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Setup.Configuration;

namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Persists setup inputs and stops the setup host so the runtime host can finish initialization.
/// </summary>
/// <remarks>
/// This is distinct from <see cref="ISetupCompletionService"/> (seeding) and 
/// <see cref="IRuntimeBootstrapInitializer"/> (runtime initialization).
/// This service handles the setup app shutdown and handoff only.
/// </remarks>
public interface ISetupBootstrapHandoffService
{
    /// <summary>
    /// Persists database and cache configuration, protects the pending seed request, marks the
    /// state configured, and requests host shutdown.
    /// </summary>
    /// <param name="request">The setup selections and sensitive values to protect and hand off.</param>
    /// <param name="cancellationToken">Cancels persistence before shutdown is requested.</param>
    /// <returns>A successful result after shutdown is requested, or a failed result containing a caught exception message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    Task<SetupBootstrapHandoffResult> CompleteAndHandoffAsync(SeedDatabaseRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes whether the setup host successfully persisted a runtime handoff.
/// </summary>
public sealed class SetupBootstrapHandoffResult
{
    /// <summary>
    /// Gets whether all persistence steps completed and host shutdown was requested.
    /// </summary>
public bool Succeeded { get; init; }
    /// <summary>
    /// Gets the errors captured from a failed handoff.
    /// </summary>
public List<string> Errors { get; init; } = [];

    /// <summary>
    /// Creates a successful handoff result.
    /// </summary>
    /// <returns>A result with <see cref="Succeeded"/> set to <see langword="true"/>.</returns>
public static SetupBootstrapHandoffResult Success() => new() { Succeeded = true };
    /// <summary>
    /// Creates a failed handoff result from one or more error messages.
    /// </summary>
    /// <param name="errors">Errors in the order supplied by the caller.</param>
    /// <returns>A result with <see cref="Succeeded"/> set to <see langword="false"/>.</returns>
public static SetupBootstrapHandoffResult Failure(params string[] errors) => new() { Succeeded = false, Errors = errors.ToList() };
}

/// <summary>
/// Coordinates the ordered, non-transactional handoff from the setup host to the main runtime host.
/// </summary>
/// <remarks>
/// Persistence steps are sequential. A failure stops the sequence and is returned without
/// requesting shutdown; earlier writes are not rolled back. Repeating the operation overwrites
/// the same bootstrap keys and pending payload. Database and cache persistence can each write
/// the configured bootstrap state before the pending request is saved, so a process failure can
/// leave configured state without a recoverable pending payload.
/// </remarks>
public sealed class SetupBootstrapHandoffService(
    IDatabaseBootstrapService databaseBootstrapService,
    ICacheBootstrapService cacheBootstrapService,
    IBootstrapPendingSetupRequestStore pendingSetupRequestStore,
    IBootstrapCompletionWriter bootstrapCompletionWriter,
    IHostApplicationLifetime hostLifetime,
    ILogger<SetupBootstrapHandoffService> logger) : ISetupBootstrapHandoffService
{
    /// <inheritdoc />
public async Task<SetupBootstrapHandoffResult> CompleteAndHandoffAsync(SeedDatabaseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!AuthenticationProviderSelections.Manager.IsCanonical(request.RequestedManagerAuthenticationProvider)
            || !AuthenticationProviderSelections.Manager.IsAvailable(request.RequestedManagerAuthenticationProvider))
        {
            return SetupBootstrapHandoffResult.Failure("The selected CMS manager authentication provider is not available.");
        }

        if (!AuthenticationProviderSelections.Member.IsCanonical(request.RequestedMemberAuthenticationProvider)
            || !AuthenticationProviderSelections.Member.IsAvailable(request.RequestedMemberAuthenticationProvider))
        {
            return SetupBootstrapHandoffResult.Failure("The selected storefront member authentication provider is not available.");
        }

        try
        {
            logger.LogInformation("Starting setup bootstrap handoff process...");

            // Step 1: Persist database bootstrap configuration
            logger.LogInformation("Persisting database bootstrap configuration...");
            await databaseBootstrapService.PersistAsync(new DatabaseBootstrapModel(
                request.DatabaseMode,
                request.ConnectionString,
                request.SecretProvider,
                request.RequestedManagerAuthenticationProvider,
                request.RequestedMemberAuthenticationProvider,
                request.InfisicalMachineId,
                request.InfisicalClientSecret,
                DatabaseUnauthenticated: request.DatabaseUnauthenticated,
                DatabaseUsername: request.DatabaseUsername,
                DatabasePassword: request.DatabasePassword
            ), cancellationToken);

            // Step 2: Persist cache bootstrap configuration
            logger.LogInformation("Persisting cache bootstrap configuration...");
            await cacheBootstrapService.PersistAsync(new CacheBootstrapModel(
                request.CacheMode,
                request.CacheConnectionString,
                request.SecretProvider,
                request.InfisicalMachineId,
                request.InfisicalClientSecret
            ), cancellationToken);

            // Step 3: Save the pending seed request for runtime initialization
            logger.LogInformation("Saving pending seed request for runtime initialization...");
            await pendingSetupRequestStore.SaveAsync(request, cancellationToken);

            // Step 4: Mark bootstrap as Configured (not Running yet - that happens after seeding)
            logger.LogInformation("Marking bootstrap state as Configured...");
            await MarkConfiguredAsync(cancellationToken);

            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            var targetPath = AppSettingsPathResolver.GetAppSettingsFilePath(env);
            logger.LogInformation("Bootstrap state persisted for environment {Environment} at {Path}. Exists={Exists}", env, targetPath, File.Exists(targetPath));

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

    /// <summary>
    /// Persists the configured state after all handoff payloads have been written.
    /// </summary>
    private async Task MarkConfiguredAsync(CancellationToken cancellationToken)
    {
        // Write Configured state to bootstrap - seeding will happen in main app
        await bootstrapCompletionWriter.MarkConfiguredAsync(cancellationToken);
    }
}
