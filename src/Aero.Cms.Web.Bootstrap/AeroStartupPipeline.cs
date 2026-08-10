using Aero.AppServer.Startup;
using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Aero.Cms.Web.Bootstrap;

/// <summary>
/// Coordinates the early configuration, setup-host handoff, and infrastructure-readiness phases of
/// Aero CMS startup.
/// </summary>
/// <remarks>
/// The setup application runs before the main host is constructed. The consumer then uses the ordinary
/// ASP.NET Core middleware, endpoint, and <c>RunAsync</c> lifecycle; runtime initialization runs as a
/// hosted service before the server accepts requests.
/// </remarks>
public static class AeroStartupPipeline
{
    /// <summary>
    /// Ensures that the normal application builder has a completed infrastructure configuration.
    /// </summary>
    /// <param name="builder">The normal ASP.NET Core application builder.</param>
    /// <param name="args">Command-line arguments forwarded to the one-time setup host.</param>
    /// <returns>The bootstrap state that the normal runtime host will start with.</returns>
    /// <remarks>
    /// When setup has not completed, a lightweight setup-only application is served using the
    /// existing setup wizard. After the wizard requests handoff, configuration is reloaded into
    /// <paramref name="builder"/> and normal host registration continues in the same process.
    /// </remarks>
    public static async Task<BootstrapState> EnsureRuntimeConfigurationAsync(
        WebApplicationBuilder builder,
        string[] args)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(args);

        Log.Information("Aero CMS starting up...");
        var state = GetBootstrapState(builder.Configuration);
        Log.Information("Bootstrap state: {State}", state.State);

        if (state.IsSetupMode)
        {
            Log.Information("Setup mode detected. Starting setup application...");
            await RunSetupAppAsync(
                args,
                builder.Configuration,
                builder.Environment.ContentRootPath,
                builder.Environment.EnvironmentName);
            state = await ReloadBootstrapStateAfterSetupAsync(builder.Configuration);
            Log.Information("Post-setup bootstrap state: {State}", state.State);
        }

        if (!state.IsConfiguredMode && !state.IsRunningMode)
        {
            throw new InvalidOperationException(
                $"Invalid bootstrap state '{state.State}' after setup. Expected Configured or Running.");
        }

        return state;
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

    private static async Task<BootstrapState> ReloadBootstrapStateAfterSetupAsync(
        ConfigurationManager configuration)
    {
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            ((IConfigurationRoot)configuration).Reload();
            var state = GetBootstrapState(configuration);

            Log.Information(
                "Bootstrap configuration reload attempt {Attempt}. State={State}",
                attempt,
                state.State);

            if (!state.IsSetupMode)
            {
                return state;
            }

            await Task.Delay(200);
        }

        ((IConfigurationRoot)configuration).Reload();
        return GetBootstrapState(configuration);
    }

    private static async Task RunSetupAppAsync(
        string[] args,
        IConfiguration earlyConfig,
        string contentRootPath,
        string environmentName)
    {
        var setupApp = await SetupAppFactory.CreateSetupAppAsync(
            args,
            earlyConfig,
            contentRootPath,
            environmentName);

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
