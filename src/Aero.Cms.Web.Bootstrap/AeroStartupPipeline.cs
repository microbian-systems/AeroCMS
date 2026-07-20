using Aero.AppServer.Startup;
using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Text.Json;

namespace Aero.Cms.Web.Bootstrap;

/// <summary>
/// Coordinates the early configuration, setup-host handoff, and infrastructure-readiness phases of
/// Aero CMS startup.
/// </summary>
/// <remarks>
/// The setup application runs before the main host is constructed. Main-host pipeline construction and
/// runtime initialization are performed separately by
/// <see cref="AeroCmsExtensions.RunAeroCmsAsync{TRootComponent}"/>.
/// </remarks>
public static class AeroStartupPipeline
{
    /// <summary>
    /// Contains configuration and bootstrap state reloaded after any setup-host handoff.
    /// </summary>
    /// <param name="Config">The early configuration snapshot.</param>
    /// <param name="State">The bootstrap state read from <paramref name="Config"/>.</param>
    /// <param name="WebProjectPath">The base path used to load application settings.</param>
    public readonly record struct EarlyStartupResult(
        IConfiguration Config,
        BootstrapState State,
        string WebProjectPath);

    /// <summary>
    /// Builds early configuration and, when setup mode is active, runs the setup application before
    /// reloading configuration and bootstrap state.
    /// </summary>
    /// <param name="args">Command-line arguments applied with the highest configuration priority.</param>
    /// <returns>
    /// A task whose result contains the configuration, bootstrap state, and web project path for main-host
    /// startup, or <see langword="null"/> when any early-startup operation fails.
    /// </returns>
    /// <remarks>
    /// In setup mode, this method waits until the setup host is told to stop, stops that host, and then
    /// retries the configuration reload up to ten times with 200-millisecond delays while setup mode
    /// remains active. Exceptions are logged as fatal and converted to a <see langword="null"/> result.
    /// </remarks>
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
    /// Builds the configuration snapshot used before the main dependency-injection container is available.
    /// </summary>
    /// <param name="args">Command-line arguments applied with the highest configuration priority.</param>
    /// <param name="webProjectPath">The base path from which JSON settings files are loaded.</param>
    /// <returns>A non-reloading configuration snapshot.</returns>
    /// <remarks>
    /// Sources are added in increasing priority: optional <c>appsettings.json</c>, optional
    /// environment-specific settings, environment variables, and command-line arguments. If
    /// <c>ASPNETCORE_ENVIRONMENT</c> is unset, the environment name defaults to <c>Development</c>.
    /// </remarks>
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
    /// Reads the current <see cref="BootstrapState"/> from a configuration snapshot.
    /// </summary>
    /// <param name="config">The configuration consumed by the bootstrap-state provider.</param>
    /// <returns>The state returned by <see cref="AppSettingsBootstrapStateProvider"/>.</returns>
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
    /// Waits for the configured database and cache infrastructure before runtime initialization.
    /// </summary>
    /// <param name="app">The started application whose services contain the resolved settings and coordinator.</param>
    /// <param name="bootstrapState">The state that determines whether readiness is required.</param>
    /// <param name="log">The logger used to record the selected infrastructure modes.</param>
    /// <returns>A task that completes when the startup coordinator reports the infrastructure ready.</returns>
    /// <remarks>
    /// Setup and other non-configured, non-running states return without resolving infrastructure
    /// services. Configured and running states use a two-minute cancellation timeout. Resolution,
    /// readiness, and timeout failures propagate to the caller.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The resolved infrastructure settings or runtime startup coordinator is not registered.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Infrastructure readiness does not complete before the two-minute timeout.
    /// </exception>
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
    /// Best-effort marks the persisted bootstrap state as failed.
    /// </summary>
    /// <param name="app">The application used to create the asynchronous service scope.</param>
    /// <param name="log">The logger that receives persistence or service-resolution failures.</param>
    /// <returns>A task that completes after the write attempt, if a completion writer is registered.</returns>
    /// <remarks>
    /// The method does nothing when <see cref="IBootstrapCompletionWriter"/> is not registered. Exceptions
    /// raised while creating the scope, resolving the writer, or persisting the state are caught and logged.
    /// </remarks>
    public static async Task TryMarkBootstrapFailedAsync(WebApplication app, Serilog.ILogger log)
    {
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var writer = scope.ServiceProvider.GetService<IBootstrapCompletionWriter>();

            if (writer is not null)
            {
                await writer.MarkFailedAsync();
            }
        }
        catch (Exception markFailedEx)
        {
            log.Error(markFailedEx, "Failed to persist bootstrap Failed state.");
        }
    }
}
