using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aero.AppServer;

/// <summary>
/// Registers logging callbacks for application lifetime transitions.
/// </summary>
/// <param name="lifetime">Provides the host lifetime cancellation tokens.</param>
/// <param name="log">The logger used by registered callbacks.</param>
internal sealed class AeroLifetimeObserver(IHostApplicationLifetime lifetime, ILogger<AeroLifetimeObserver> log) : IHostedService
{
    /// <summary>
    /// Attaches callbacks to the application started, stopping, and stopped tokens.
    /// </summary>
    /// <param name="cancellationToken">Unused; registration is synchronous.</param>
    /// <returns>A completed task after callbacks are registered.</returns>
public Task StartAsync(CancellationToken cancellationToken)
    {
        // Register the callbacks
        lifetime.ApplicationStarted.Register(() =>
            log.LogInformation("Aero.AppServer has fully started."));

        lifetime.ApplicationStopping.Register(() =>
            log.LogInformation("Aero.AppServer is stopping... Cleaning up hosted application services."));

        lifetime.ApplicationStopped.Register(() =>
            log.LogInformation("Aero.AppServer is fully stopped."));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Completes without additional shutdown work.
    /// </summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
