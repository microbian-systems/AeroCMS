using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MysticMind.PostgresEmbed;
using Aero.AppServer.Startup;
using Npgsql;
using System.Data.Common;

namespace Aero.AppServer;

public class AeroEmbeddedDbService(
    IOptionsMonitor<AeroDbOptions> embeddedOptions,
    ILogger<AeroEmbeddedDbService> log,
    IInfrastructureReadinessSnapshot readiness,
    IMultiStartupSignal startupSignal) : BackgroundService
{
    PgServer? server;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Aero embedded PostgreSQL server is running...");
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        log.LogInformation("AeroEmbedDbService: Starting Aero embedded PostgreSQL server...");
        var current = embeddedOptions.CurrentValue;
        var pgPort = current.Port;
        var pgVersion = current.PgVersion;

        var serverParams = new Dictionary<string, string>();

        // todo - add pg_vector
        // todo - configure embedded sql server to only allow connections from localhost
        serverParams.Add("timezone", "UTC");
        serverParams.Add("listen_addresses", "127.0.0.1");
        server = new PgServer(
            pgVersion, 
            instanceId:Guid.Empty, 
            pgUser: AeroAppServerConstants.EmbeddedDbUser, 
            port: pgPort, 
            pgServerParams: serverParams,
            clearInstanceDirOnStop: false,
            clearWorkingDirOnStart: false);
        await server.StartAsync();
        await Task.Run(async () =>
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested) // todo - do we need a while clause for the embedded pg server? 
                {
                    try
                    {
                        var masterConn = AeroAppServerConstants.EmbedConnString
                            .Replace("Database=aero", "Database=postgres")
                            ;
                        
                        await using var db = new NpgsqlConnection(masterConn);
                        await db.OpenAsync(cancellationToken);

                        // 1. Ensure 'aero' user exists
                        var userExists = await new NpgsqlCommand(
                            "SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'aero')", db)
                            .ExecuteScalarAsync(cancellationToken) is bool b1 && b1;

                        if (!userExists)
                        {
                            log.LogInformation("Creating 'aero' database user...");
                            await new NpgsqlCommand("CREATE ROLE aero WITH LOGIN SUPERUSER", db)
                            .ExecuteNonQueryAsync(cancellationToken);
                        }

                        // 2. Ensure 'aero' database exists
                        var dbExists = await new NpgsqlCommand(
                            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'aero')", db)
                            .ExecuteScalarAsync(cancellationToken) is bool b2 && b2;

                        if (!dbExists)
                        {
                            log.LogInformation("Creating 'aero' database...");
                            await new NpgsqlCommand("CREATE DATABASE aero OWNER aero", db)
                            .ExecuteNonQueryAsync(cancellationToken);
                        }

                        // 3. Log version
                        var version = (string?)await new NpgsqlCommand("SELECT version();", db)
                        .ExecuteScalarAsync(cancellationToken);
                        log.LogInformation("PostgreSQL version: {Version}", version);

                        readiness.PostgresReady = true;
                        startupSignal.MarkReady(StartupServiceNames.Postgres);
                        log.LogInformation("Embedded PostgreSQL is ready on port {Port}.", pgPort);
                        return;
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        log.LogError(ex, "Embedded PostgreSQL not ready yet on port {Port}.", pgPort);
                        await Task.Delay(500, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Aero embedded PostgreSQL readiness check failed.");
            }
        }, cancellationToken);

        await base.StartAsync(cancellationToken);
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
