using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MysticMind.PostgresEmbed;
using Aero.AppServer.Startup;

namespace Aero.AppServer;

public class AeroEmbeddedDbService(
    IConfiguration config,
    ILogger<AeroEmbeddedDbService> log,
    IInfrastructureReadinessSnapshot readiness,
    IMultiStartupSignal startupSignal) : BackgroundService
{
    PgServer? server;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Aero embedded PostgreSQL server is running...");
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        log.LogInformation("AeroEmbedDbService: Starting Aero embedded PostgreSQL server...");
        var pgPort = config.GetValue<int?>("Aero:Embedded:Port") ?? AeroAppServerConstants.PgPort;
        var pgVersion = config.GetValue<string>("Aero:Embedded:PgVersion") ?? AeroAppServerConstants.PgVersion;

        server = new PgServer(pgVersion, port: pgPort);
        server.Start();
        _ = Task.Run(async () =>
        {
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync("127.0.0.1", pgPort, cancellationToken);
                readiness.PostgresReady = true;
                startupSignal.MarkReady(StartupServiceNames.Postgres);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Aero embedded PostgreSQL readiness check failed.");
            }
        }, cancellationToken);

        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        log.LogInformation("AeroEmbedDbService: Stopping Aero embedded PostgreSQL server...");
        server?.Stop();
        return base.StopAsync(cancellationToken);
    }

    private void OnStarted(CancellationToken token)
    {
        log.LogInformation("AeroEmbedDbService: Application has fully started and Aero cache is listening.");
    }

    private void OnStopping()
    {
        log.LogInformation("AeroEmbedDbService: Application is shutting down. Preparing to stop Aero cache...");
    }

    private void OnStopped()
    {
        log.LogInformation("AeroEmbedDbService: Application has stopped. Aero cache resources released.");
    }

    public override void Dispose()
    {
        log.LogInformation("AeroEmbedDbService: Disposing Aero cache server...");
        server?.Stop();
        server?.Dispose();
        base.Dispose();
    }
}
