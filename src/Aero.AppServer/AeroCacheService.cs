using Garnet;
using Aero.AppServer.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using Garnet.server;
using System.Net;

namespace Aero.AppServer;

internal sealed class AeroCacheService(
        IConfiguration config,
        ILogger<AeroCacheService> log,
        IInfrastructureReadinessSnapshot readiness,
        IMultiStartupSignal startupSignal) : BackgroundService
{
    private GarnetServer? server;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Aero Caching Server is running...");

        

        return Task.CompletedTask;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var port = config.GetValue("Aero:Cache:Port", AeroAppServerConstants.CachePort);

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
                        log.LogInformation("Embedded Garnet cache is ready on port {Port}.", port);
                        return;
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        log.LogDebug(ex, "Embedded Garnet cache not ready yet on port {Port}.", port);
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

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        log.LogInformation("Stopping Aero cache server...");
        return base.StopAsync(cancellationToken);
    }

    private void OnStarted()
    {
        log.LogInformation("AeroCacheService: Application has fully started and Aero cache is listening.");
    }

    private void OnStopping()
    {
        log.LogInformation("AeroCacheService: Application is shutting down. Preparing to stop Aero cache...");
    }

    private void OnStopped()
    {
        log.LogInformation("AeroCacheService: Application has stopped. Aero cache resources released.");
    }

    public override void Dispose()
    {
        log.LogInformation("AeroCacheService: Disposing Aero cache server...");
        server?.Dispose();
        base.Dispose();
    }
}
