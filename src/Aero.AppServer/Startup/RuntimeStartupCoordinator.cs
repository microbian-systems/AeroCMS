namespace Aero.AppServer.Startup;

public interface IRuntimeStartupCoordinator
{
    Task WaitForInfrastructureAsync(ResolvedInfrastructureSettings settings, CancellationToken cancellationToken = default);
}

public sealed class RuntimeStartupCoordinator(
    IMultiStartupSignal startupSignal,
    IInfrastructureReadinessSnapshot readinessSnapshot) : IRuntimeStartupCoordinator
{
    public async Task WaitForInfrastructureAsync(ResolvedInfrastructureSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var requiredServices = new List<string>();

        if (settings.DatabaseMode.Equals("Embedded", StringComparison.OrdinalIgnoreCase))
        {
            requiredServices.Add(StartupServiceNames.Postgres);
        }

        if (settings.CacheMode.Equals("Embedded", StringComparison.OrdinalIgnoreCase))
        {
            requiredServices.Add(StartupServiceNames.Garnet);
        }

        readinessSnapshot.DatabaseMode = settings.DatabaseMode;
        readinessSnapshot.CacheMode = settings.CacheMode;
        readinessSnapshot.SecretProvider = settings.SecretProvider;

        if (requiredServices.Count == 0)
        {
            return;
        }

        await startupSignal.WaitForAllAsync(requiredServices, cancellationToken);
    }
}
