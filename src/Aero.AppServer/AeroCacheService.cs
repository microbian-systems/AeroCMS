using Garnet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aero.AppServer;

internal sealed class AeroCacheService(
        IConfiguration config,
        ILogger<AeroCacheService> log,
        IHostApplicationLifetime lifetime) : BackgroundService
{
    private GarnetServer? server;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Aero Caching Server is running...");

        

        return Task.CompletedTask;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var port = config.GetValue<int>("Aero:Cache:Port", AeroAppServerConstants.CachePort);
        server = new GarnetServer(["--port", port.ToString()]);

        log.LogInformation("Starting Aero cache server on port {port}...", port);
        server.Start();
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