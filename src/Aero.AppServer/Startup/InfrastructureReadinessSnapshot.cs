namespace Aero.AppServer.Startup;

/// <summary>
/// Stores the current process's bootstrap and infrastructure readiness state.
/// </summary>
public sealed class InfrastructureReadinessSnapshot : IInfrastructureReadinessSnapshot
{
    /// <inheritdoc />
public bool SetupComplete { get; set; }
    /// <inheritdoc />
public bool SeedComplete { get; set; }
    /// <inheritdoc />
public bool HasBootstrapConfig { get; set; }
    /// <inheritdoc />
public string? DatabaseMode { get; set; }
    /// <inheritdoc />
public string? CacheMode { get; set; }
    /// <inheritdoc />
public string? SecretProvider { get; set; }
    /// <inheritdoc />
        public bool AeroDbReady { get; set; }
    /// <inheritdoc />
public bool GarnetReady { get; set; }
}
