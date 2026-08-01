namespace Aero.AppServer.Startup;

/// <summary>
/// Exposes mutable bootstrap and local-infrastructure readiness state for the current process.
/// </summary>
public interface IInfrastructureReadinessSnapshot
{
    /// <summary>
    /// Gets or sets whether interactive setup has completed.
    /// </summary>
bool SetupComplete { get; set; }
    /// <summary>
    /// Gets or sets whether initial data seeding has completed.
    /// </summary>
bool SeedComplete { get; set; }
    /// <summary>
    /// Gets or sets whether persisted bootstrap configuration is available.
    /// </summary>
bool HasBootstrapConfig { get; set; }
    /// <summary>
    /// Gets or sets the resolved database mode.
    /// </summary>
string? DatabaseMode { get; set; }
    /// <summary>
    /// Gets or sets the resolved cache mode.
    /// </summary>
string? CacheMode { get; set; }
    /// <summary>
    /// Gets or sets the resolved secret-provider label.
    /// </summary>
string? SecretProvider { get; set; }
    /// <summary>
    /// Gets or sets whether the embedded AeroDB service has signaled readiness.
    /// </summary>
        bool AeroDbReady { get; set; }
    /// <summary>
    /// Gets or sets whether the local Garnet service has passed its TCP readiness probe.
    /// </summary>
bool GarnetReady { get; set; }
}
