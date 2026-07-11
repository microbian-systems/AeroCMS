using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Aero.AppServer.Startup;

namespace Aero.AppServer;

/// <summary>
/// Represents a class for AeroEmbeddedDbService.
/// </summary>
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
    /// ExecuteAsync method.
    /// </summary>
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("AeroEmbedDbService: Sable KV embedded store running in-process...");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

        /// <summary>
    /// StartAsync method.
    /// </summary>
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
    /// StopAsync method.
    /// </summary>
public override Task StopAsync(CancellationToken cancellationToken)
    {
        log.LogInformation("AeroEmbedDbService: Stopping Aero embedded SurrealDB service...");
        return base.StopAsync(cancellationToken);
    }

        /// <summary>
    /// Dispose method.
    /// </summary>
public override void Dispose()
    {
        log.LogInformation("AeroEmbedDbService: Disposing Aero embedded SurrealDB resources...");
        base.Dispose();
    }
}
