using Garnet;
using Aero.AppServer.Startup;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using Garnet.server;
using System.Net;

namespace Aero.AppServer;

/// <summary>
/// Hosts the process-local Garnet cache and publishes its readiness state.
/// </summary>
/// <param name="log">The logger for lifecycle and readiness-probe events.</param>
/// <param name="readiness">The mutable process readiness snapshot.</param>
/// <param name="startupSignal">The named readiness-signal registry.</param>
/// <remarks>
/// The server listens only on loopback. Startup returns after launching an asynchronous TCP
/// readiness probe; consumers that require the cache must wait on <see cref="IMultiStartupSignal"/>.
/// </remarks>
internal sealed class AeroCacheService(
        ILogger<AeroCacheService> log,
        IInfrastructureReadinessSnapshot readiness,
        IMultiStartupSignal startupSignal) : BackgroundService
{
    private GarnetServer? server;

    /// <summary>
    /// Records that the background service execution loop has started.
    /// </summary>
    /// <param name="stoppingToken">The host shutdown token.</param>
    /// <returns>A completed task; Garnet is managed by <see cref="StartAsync"/>.</returns>
protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Aero Caching Server is running...");

        

        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts Garnet and launches a loopback readiness probe.
    /// </summary>
    /// <param name="cancellationToken">Cancels the readiness probe and base-service startup.</param>
    /// <returns>The base hosted-service startup task.</returns>
    /// <remarks>
    /// The probe retries every 500 milliseconds. A successful connection updates both readiness
    /// abstractions; probe failures are logged and do not fail the returned startup task.
    /// </remarks>
public override Task StartAsync(CancellationToken cancellationToken)
    {
        const int port = AeroAppServerConstants.CachePort;

        // 1. Define your limits in bytes/counts
        var indexSize = 128 * 1024 * 1024;       // 128 MB (Main Index)
        var memorySize = 128 * 1024 * 1024;     // 128 MB (Main Log)
        var objIndexSize = 32 * 1024 * 1024;    // 32 MB (Object Index)
        var objLogSize = 32 * 1024 * 1024;      // 32 MB (Object Log)
        var objHeapSize = 32 * 1024 * 1024;     // 32 MB (Object Heap)

        // 2. Configure the server options
        var options = new GarnetServerOptions
        {
            IndexSize = indexSize.ToString(),
            IndexMaxSize = (indexSize * 2).ToString(),
            MemorySize = memorySize.ToString(),
            ObjectStoreIndexSize = objIndexSize.ToString(),
            // Log memory for objects is defined by size in bytes
            ObjectStoreLogMemorySize = objLogSize.ToString(), 
            ObjectStoreHeapMemorySize = objHeapSize.ToString(),
            
            // Optional: Smaller page size helps rotation in low-memory environments
            PageSize = "4m", 
            ObjectStorePageSize = "4m",
            
            // If you don't need complex types (Lists, Sets), uncomment the next line:
            // DisableObjects = true 
            EndPoints = new IPEndPoint[] { new IPEndPoint(IPAddress.Loopback, port) }
        };

        server = new GarnetServer(options);

        log.LogInformation("Starting Aero cache server on port {port}...", port);
        server.Start();
        _ = Task.Run(async () =>
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        using var client = new TcpClient();
                        await client.ConnectAsync(AeroAppServerConstants.CacheHost, port, cancellationToken);
                        readiness.GarnetReady = true;
                        startupSignal.MarkReady(StartupServiceNames.Garnet);
                        log.LogInformation("Local Garnet cache is ready on port {Port}.", port);
                        return;
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        log.LogDebug(ex, "Local Garnet cache not ready yet on port {Port}.", port);
                        await Task.Delay(500, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Aero cache readiness check failed.");
            }
        }, cancellationToken);
        return base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Requests background-service shutdown.
    /// </summary>
    /// <param name="cancellationToken">The token limiting graceful shutdown.</param>
    /// <returns>The base hosted-service shutdown task.</returns>
public override Task StopAsync(CancellationToken cancellationToken)
    {
        log.LogInformation("Stopping Aero cache server...");
        return base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Logs the application-started lifecycle transition.
    /// </summary>
    private void OnStarted()
    {
        log.LogInformation("AeroCacheService: Application has fully started and Aero cache is listening.");
    }

    /// <summary>
    /// Logs the application-stopping lifecycle transition.
    /// </summary>
    private void OnStopping()
    {
        log.LogInformation("AeroCacheService: Application is shutting down. Preparing to stop Aero cache...");
    }

    /// <summary>
    /// Logs the application-stopped lifecycle transition.
    /// </summary>
    private void OnStopped()
    {
        log.LogInformation("AeroCacheService: Application has stopped. Aero cache resources released.");
    }

    /// <summary>
    /// Disposes the Garnet server before releasing hosted-service resources.
    /// </summary>
public override void Dispose()
    {
        log.LogInformation("AeroCacheService: Disposing Aero cache server...");
        server?.Dispose();
        base.Dispose();
    }
}
