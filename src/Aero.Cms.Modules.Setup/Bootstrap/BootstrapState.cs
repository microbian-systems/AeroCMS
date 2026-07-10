namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Represents a class for BootstrapStates.
/// </summary>
public static class BootstrapStates
{
        /// <summary>
    /// Setup.
    /// </summary>
public const string Setup = "Setup";
        /// <summary>
    /// Configured.
    /// </summary>
public const string Configured = "Configured";
        /// <summary>
    /// Running.
    /// </summary>
public const string Running = "Running";
        /// <summary>
    /// Failed.
    /// </summary>
public const string Failed = "Failed";
}

/// <summary>
/// Represents a class for BootstrapState.
/// </summary>
public sealed class BootstrapState
{
        /// <summary>
    /// Gets or sets the State.
    /// </summary>
public string State { get; set; } = BootstrapStates.Setup;
        /// <summary>
    /// Gets or sets the Setup Complete.
    /// </summary>
public bool SetupComplete { get; set; }
        /// <summary>
    /// Gets or sets the Seed Complete.
    /// </summary>
public bool SeedComplete { get; set; }
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
    /// Gets or sets the Authentication Mode.
    /// </summary>
public string? AuthenticationMode { get; set; }
        /// <summary>
    /// Gets or sets the Has Bootstrap Config.
    /// </summary>
public bool HasBootstrapConfig { get; set; }

        /// <summary>
    /// Gets or sets the Is Setup Mode.
    /// </summary>
public bool IsSetupMode => string.Equals(State, BootstrapStates.Setup, StringComparison.OrdinalIgnoreCase);

        /// <summary>
    /// Gets or sets the Is Configured Mode.
    /// </summary>
public bool IsConfiguredMode => string.Equals(State, BootstrapStates.Configured, StringComparison.OrdinalIgnoreCase);

        /// <summary>
    /// Gets or sets the Is Running Mode.
    /// </summary>
public bool IsRunningMode => string.Equals(State, BootstrapStates.Running, StringComparison.OrdinalIgnoreCase);
}
