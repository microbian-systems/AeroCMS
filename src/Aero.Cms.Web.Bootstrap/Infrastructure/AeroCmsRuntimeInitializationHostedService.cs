using Aero.AppServer.Startup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Modular;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Web.Bootstrap.Infrastructure;

/// <summary>
/// Completes infrastructure readiness, deferred setup, and module initialization as part of
/// the standard ASP.NET Core host startup lifecycle.
/// </summary>
internal sealed class AeroCmsRuntimeInitializationHostedService(
    IBootstrapStateProvider bootstrapStateProvider,
    ResolvedInfrastructureSettings infrastructure,
    IRuntimeStartupCoordinator startupCoordinator,
    RuntimeBootstrapReadinessGate readinessGate,
    IServiceProvider rootServices,
    ILogger<AeroCmsRuntimeInitializationHostedService> logger) : IHostedService
{
    private static readonly TimeSpan InfrastructureTimeout = TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bootstrapState = bootstrapStateProvider.GetState();
        if (!bootstrapState.IsConfiguredMode && !bootstrapState.IsRunningMode)
        {
            throw new InvalidOperationException(
                $"The Aero CMS runtime host cannot start in bootstrap state '{bootstrapState.State}'.");
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(InfrastructureTimeout);

            logger.LogInformation(
                "Waiting for Aero CMS infrastructure. DatabaseMode={DatabaseMode}, CacheMode={CacheMode}",
                infrastructure.DatabaseMode,
                infrastructure.CacheMode);
            await startupCoordinator.WaitForInfrastructureAsync(infrastructure, timeout.Token);

            await using var scope = rootServices.CreateAsyncScope();
            if (bootstrapState.IsConfiguredMode)
            {
                logger.LogInformation("Completing deferred Aero CMS setup handoff.");
                var initializer = scope.ServiceProvider.GetRequiredService<IRuntimeBootstrapInitializer>();
                await initializer.InitializeAsync(timeout.Token);
            }

            await InitializeModulesAsync(scope.ServiceProvider);
            readinessGate.SignalReady();
            logger.LogInformation("Aero CMS runtime initialization completed.");
        }
        catch (Exception exception)
        {
            readinessGate.SignalFailure();
            logger.LogCritical(exception, "Aero CMS runtime initialization failed.");

            if (bootstrapState.IsConfiguredMode)
            {
                await TryMarkBootstrapFailedAsync();
            }

            // Throwing from StartAsync prevents the web server from accepting requests with a
            // partially initialized CMS runtime.
            throw;
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task InitializeModulesAsync(IServiceProvider scopedServices)
    {
        var graph = rootServices.GetService<ModuleGraph>();
        if (graph is not null)
        {
            foreach (var descriptor in graph.LoadOrder)
            {
                if (rootServices.GetService(descriptor.ModuleType) is IAeroModule module)
                {
                    await module.RunAsync(scopedServices);
                }
            }

            return;
        }

        foreach (var module in rootServices.GetServices<IAeroModule>().OrderBy(module => module.Order))
        {
            await module.RunAsync(scopedServices);
        }
    }

    private async Task TryMarkBootstrapFailedAsync()
    {
        try
        {
            await using var scope = rootServices.CreateAsyncScope();
            var writer = scope.ServiceProvider.GetService<IBootstrapCompletionWriter>();
            if (writer is not null)
            {
                await writer.MarkFailedAsync();
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to persist the failed Aero CMS bootstrap state.");
        }
    }
}
