using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Aero.AppServer.Startup;

namespace Aero.AppServer;

/// <summary>
/// Publishes readiness and lifetime signals for the in-process SurrealKV database.
/// </summary>
/// <param name="log">The logger for lifecycle events.</param>
/// <param name="readiness">The mutable process readiness snapshot.</param>
/// <param name="startupSignal">The named readiness-signal registry.</param>
/// <remarks>
/// SurrealDB embedded (Sable) runs in-process via SurrealDbKvClient with zero startup time.
/// This service exists to signal readiness to the <see cref="RuntimeStartupCoordinator"/>
/// and maintain symmetry with other infrastructure services like <see cref="AeroCacheService"/>.
/// </remarks>
public class AeroEmbeddedDbService(
    ILogger<AeroEmbeddedDbService> log,
    IInfrastructureReadinessSnapshot readiness,
    IMultiStartupSignal startupSignal) : BackgroundService
{
    /// <summary>
    /// Keeps the hosted service alive until application shutdown.
    /// </summary>
    /// <param name="stoppingToken">Cancels the indefinite wait.</param>
    /// <returns>A task that completes through cancellation during normal shutdown.</returns>
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("AeroEmbedDbService: Sable KV embedded store running in-process...");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    /// <summary>
    /// Marks the embedded database ready before starting the background-service lifetime.
    /// </summary>
    /// <param name="cancellationToken">The host startup cancellation token.</param>
    /// <returns>A task that completes when the base hosted service has started.</returns>
public override async Task StartAsync(CancellationToken cancellationToken)
    {
        log.LogInformation("AeroEmbedDbService: Signaling Sable KV readiness (in-process, zero startup)");

        // SurrealDB embedded has zero instantiation delay.
        // Signal readiness immediately - no process to start, no polling needed.
        readiness.AeroDbReady = true;
        startupSignal.MarkReady(StartupServiceNames.AeroDb);

        log.LogInformation("AeroEmbedDbService: Sable KV embedded store ready.");

        await base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Requests shutdown of the embedded-database lifetime service.
    /// </summary>
    /// <param name="cancellationToken">The token limiting graceful shutdown.</param>
    /// <returns>The base hosted-service shutdown task.</returns>
public override Task StopAsync(CancellationToken cancellationToken)
    {
        log.LogInformation("AeroEmbedDbService: Stopping Aero embedded SurrealDB service...");
        return base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Releases inherited background-service resources.
    /// </summary>
public override void Dispose()
    {
        log.LogInformation("AeroEmbedDbService: Disposing Aero embedded SurrealDB resources...");
        base.Dispose();
    }
}
