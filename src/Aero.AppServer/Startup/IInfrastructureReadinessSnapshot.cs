namespace Aero.AppServer.Startup;

/// <summary>
/// Defines an interface for IInfrastructureReadinessSnapshot.
/// </summary>
public interface IInfrastructureReadinessSnapshot
{
        /// <summary>
    /// Gets or sets the Setup Complete.
    /// </summary>
bool SetupComplete { get; set; }
        /// <summary>
    /// Gets or sets the Seed Complete.
    /// </summary>
bool SeedComplete { get; set; }
        /// <summary>
    /// Gets or sets the Has Bootstrap Config.
    /// </summary>
bool HasBootstrapConfig { get; set; }
        /// <summary>
    /// Gets or sets the Database Mode.
    /// </summary>
string? DatabaseMode { get; set; }
        /// <summary>
    /// Gets or sets the Cache Mode.
    /// </summary>
string? CacheMode { get; set; }
        /// <summary>
    /// Gets or sets the Secret Provider.
    /// </summary>
string? SecretProvider { get; set; }
        /// <summary>
        /// Gets or sets the AeroDb Ready.
        /// </summary>
        bool AeroDbReady { get; set; }
        /// <summary>
    /// Gets or sets the Garnet Ready.
    /// </summary>
bool GarnetReady { get; set; }
}
