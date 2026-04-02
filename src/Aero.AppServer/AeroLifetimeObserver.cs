using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aero.AppServer;

internal sealed class AeroLifetimeObserver(IHostApplicationLifetime lifetime, ILogger<AeroLifetimeObserver> log) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Register the callbacks
        lifetime.ApplicationStarted.Register(() =>
            log.LogInformation("Aero.AppServer has fully started."));

        lifetime.ApplicationStopping.Register(() =>
            log.LogInformation("Aero.AppServer is stopping... Cleaning up Orleans and Garnet."));

        lifetime.ApplicationStopped.Register(() =>
            log.LogInformation("Aero.AppServer is fully stopped."));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
