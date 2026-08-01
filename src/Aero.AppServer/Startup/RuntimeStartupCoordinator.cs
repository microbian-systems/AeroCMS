namespace Aero.AppServer.Startup;

/// <summary>
/// Waits for the process-local infrastructure required by a resolved runtime configuration.
/// </summary>
public interface IRuntimeStartupCoordinator
{
    /// <summary>
    /// Records resolved modes and waits for any selected embedded database or local cache.
    /// </summary>
    /// <param name="settings">The resolved infrastructure modes and connection settings.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes once all required local services are ready.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="settings"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is canceled before readiness.
    /// </exception>
Task WaitForInfrastructureAsync(ResolvedInfrastructureSettings settings, CancellationToken cancellationToken = default);
}

/// <summary>
/// Coordinates runtime startup against the named readiness-signal registry.
/// </summary>
/// <param name="startupSignal">The registry used to await named local services.</param>
/// <param name="readinessSnapshot">The snapshot updated with resolved mode metadata.</param>
public sealed class RuntimeStartupCoordinator(
    IMultiStartupSignal startupSignal,
    IInfrastructureReadinessSnapshot readinessSnapshot) : IRuntimeStartupCoordinator
{
    /// <inheritdoc />
public async Task WaitForInfrastructureAsync(ResolvedInfrastructureSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var requiredServices = new List<string>();

        if (settings.DatabaseMode.Equals("Embedded", StringComparison.OrdinalIgnoreCase))
        {
            requiredServices.Add(StartupServiceNames.AeroDb);
        }

        if (settings.CacheMode.Equals(AeroAppServerConstants.LocalCacheMode, StringComparison.OrdinalIgnoreCase))
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
