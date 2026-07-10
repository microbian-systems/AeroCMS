namespace Aero.AppServer.Startup;

/// <summary>
/// Represents a class for InfrastructureReadinessSnapshot.
/// </summary>
public sealed class InfrastructureReadinessSnapshot : IInfrastructureReadinessSnapshot
{
        /// <summary>
    /// Gets or sets the Setup Complete.
    /// </summary>
public bool SetupComplete { get; set; }
        /// <summary>
    /// Gets or sets the Seed Complete.
    /// </summary>
public bool SeedComplete { get; set; }
        /// <summary>
    /// Gets or sets the Has Bootstrap Config.
    /// </summary>
public bool HasBootstrapConfig { get; set; }
        /// <summary>
    /// Gets or sets the Database Mode.
    /// </summary>
public string? DatabaseMode { get; set; }
        /// <summary>
    /// Gets or sets the Cache Mode.
    /// </summary>
public string? CacheMode { get; set; }
        /// <summary>
    /// Gets or sets the Secret Provider.
    /// </summary>
public string? SecretProvider { get; set; }
        /// <summary>
    /// Gets or sets the Postgres Ready.
    /// </summary>
public bool PostgresReady { get; set; }
        /// <summary>
    /// Gets or sets the Garnet Ready.
    /// </summary>
public bool GarnetReady { get; set; }
}
