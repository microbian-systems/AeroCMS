using Aero.AppServer;
using Aero.AppServer.Startup;
using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.IO;
using System.Text.Json;

namespace Aero.Cms.Web.Bootstrap;

/// <summary>
/// Handles the AeroCMS two-stage startup pattern:
/// 1. Setup App — Runs with minimal DI when bootstrap state is "Setup"
/// 2. Main App  — Runs with full DI after setup completes
///
/// The setup wizard runs before database/cache infrastructure is initialized,
/// and enables automatic transition via IHostApplicationLifetime.StopApplication().
/// </summary>
public static class AeroStartupPipeline
{
    /// <summary>
    /// Result of the early bootstrap phases. Callers use this to decide
    /// whether to proceed to the main application phase.
    /// </summary>
    public readonly record struct EarlyStartupResult(
        IConfiguration Config,
        BootstrapState State,
        string WebProjectPath);

    /// <summary>
    /// Runs Phases 1 &amp; 2 of the startup pipeline:
    /// Phase 1 — Build early configuration and check bootstrap state.
    /// Phase 2 — Run the Setup App if bootstrap state is "Setup".
    ///
    /// Returns null if setup fails irrecoverably. Returns a valid result
    /// when the application should proceed to the Main App phase (Phases 3+).
    /// </summary>
    public static async Task<EarlyStartupResult?> RunEarlyPhasesAsync(string[] args)
    {
        var webProjectPath = Aero.Cms.Modules.Setup.Configuration.AppSettingsPathResolver.GetWebProjectPath();

        try
        {
            Log.Information("Aero CMS starting up...");

            // Phase 1: Build early configuration and check bootstrap state
            var earlyConfig = BuildEarlyConfiguration(args, webProjectPath);
            var bootstrapState = GetBootstrapState(earlyConfig);

            Log.Information("Bootstrap state: {State}", bootstrapState.State);

            // Phase 2: Run Setup App if needed
            if (bootstrapState.IsSetupMode)
            {
                Log.Information("Setup mode detected. Starting setup application...");
                await RunSetupAppAsync(args, earlyConfig);

                // Re-read configuration after setup app exits
                Log.Information("Setup application completed. Re-reading configuration...");
                (earlyConfig, bootstrapState) = await ReloadBootstrapStateAfterSetupAsync(args, webProjectPath);

                Log.Information("Post-setup bootstrap state: {State}", bootstrapState.State);
            }

            return new EarlyStartupResult(earlyConfig, bootstrapState, webProjectPath);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly during early startup phases");
            return null;
        }
    }

    /// <summary>
    /// Builds an early <see cref="IConfiguration"/> before the full DI container is available,
    /// used to read bootstrap state from appsettings.
    /// </summary>
    public static IConfiguration BuildEarlyConfiguration(string[] args, string webProjectPath)
    {
        var configBuilder = new ConfigurationBuilder();

        configBuilder.SetBasePath(webProjectPath);
        configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        configBuilder.AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false);

        configBuilder.AddEnvironmentVariables();
        configBuilder.AddCommandLine(args);

        return configBuilder.Build();
    }

    /// <summary>
    /// Reads the <see cref="BootstrapState"/> from configuration.
    /// </summary>
    public static BootstrapState GetBootstrapState(IConfiguration config)
    {
        var provider = new AppSettingsBootstrapStateProvider(config);
        return provider.GetState();
    }

    private static async Task<(IConfiguration Config, BootstrapState State)> ReloadBootstrapStateAfterSetupAsync(string[] args, string webProjectPath)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var envPath = Path.Combine(webProjectPath, $"appsettings.{env}.json");

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            var config = BuildEarlyConfiguration(args, webProjectPath);
            var state = GetBootstrapState(config);

            Log.Information(
                "Bootstrap reread attempt {Attempt}. Environment={Environment}, File={FilePath}, State={State}",
                attempt,
                env,
                envPath,
                state.State);

            if (!state.IsSetupMode)
            {
                return (config, state);
            }

            if (File.Exists(envPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(envPath);
                    using var document = JsonDocument.Parse(json);

                    if (document.RootElement.TryGetProperty("AeroCms", out var aeroCms) &&
                        aeroCms.TryGetProperty("Bootstrap", out var bootstrap) &&
                        bootstrap.TryGetProperty("State", out var rawState))
                    {
                        Log.Warning(
                            "Bootstrap file still reports State={RawState} on reread attempt {Attempt}.",
                            rawState.GetString(),
                            attempt);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed reading bootstrap file directly during reread attempt {Attempt}.", attempt);
                }
            }

            await Task.Delay(200);
        }

        var finalConfig = BuildEarlyConfiguration(args, webProjectPath);
        return (finalConfig, GetBootstrapState(finalConfig));
    }

    private static async Task RunSetupAppAsync(string[] args, IConfiguration earlyConfig)
    {
        var setupApp = await SetupAppFactory.CreateSetupAppAsync(args, earlyConfig);

        await setupApp.StartAsync();

        try
        {
            Log.Information("Setup application started. Waiting for setup completion...");
            // Block here until StopApplication() is called by SetupBootstrapHandoffService
            await setupApp.WaitForShutdownAsync();
            Log.Information("Setup application received shutdown signal.");
        }
        finally
        {
            await setupApp.StopAsync();
            Log.Information("Setup application stopped.");
        }
    }

    /// <summary>
    /// Waits for required infrastructure (database, cache) to be ready before
    /// proceeding with runtime initialization in the main application phase.
    /// </summary>
    public static async Task WaitForRequiredInfrastructureAsync(WebApplication app, BootstrapState bootstrapState, Serilog.ILogger log)
    {
        if (!bootstrapState.IsConfiguredMode && !bootstrapState.IsRunningMode)
        {
            return;
        }

        var resolvedInfrastructure = app.Services.GetRequiredService<ResolvedInfrastructureSettings>();
        var startupCoordinator = app.Services.GetRequiredService<IRuntimeStartupCoordinator>();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        log.Information(
            "Waiting for required infrastructure. DatabaseMode={DatabaseMode}, CacheMode={CacheMode}",
            resolvedInfrastructure.DatabaseMode,
            resolvedInfrastructure.CacheMode);

        await startupCoordinator.WaitForInfrastructureAsync(resolvedInfrastructure, cts.Token);
    }

    /// <summary>
    /// Attempts to mark the bootstrap state as Failed when an unrecoverable
    /// error occurs during the main application startup in Configured mode.
    /// </summary>
    public static async Task TryMarkBootstrapFailedAsync(WebApplication app, Serilog.ILogger log)
    {
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var writer = scope.ServiceProvider.GetService<IBootstrapCompletionWriter>();

            if (writer is not null)
            {
                await writer.MarkFailedAsync();
                log.Warning("Bootstrap state marked as Failed.");
            }
        }
        catch (Exception markFailedEx)
        {
            log.Error(markFailedEx, "Failed to persist bootstrap Failed state.");
        }
    }
}
