namespace Aero.AppServer.Startup;

/// <summary>
/// Defines an interface for IRuntimeStartupCoordinator.
/// </summary>
public interface IRuntimeStartupCoordinator
{
        /// <summary>
    /// WaitForInfrastructureAsync method.
    /// </summary>
Task WaitForInfrastructureAsync(ResolvedInfrastructureSettings settings, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a class for RuntimeStartupCoordinator.
/// </summary>
public sealed class RuntimeStartupCoordinator(
    IMultiStartupSignal startupSignal,
    IInfrastructureReadinessSnapshot readinessSnapshot) : IRuntimeStartupCoordinator
{
        /// <summary>
    /// WaitForInfrastructureAsync method.
    /// </summary>
public async Task WaitForInfrastructureAsync(ResolvedInfrastructureSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var requiredServices = new List<string>();

        if (settings.DatabaseMode.Equals("Embedded", StringComparison.OrdinalIgnoreCase))
        {
            requiredServices.Add(StartupServiceNames.AeroDb);
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
