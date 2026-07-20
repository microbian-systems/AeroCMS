namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Defines the persisted lifecycle values understood by the setup gate and runtime initializer.
/// </summary>
public static class BootstrapStates
{
    /// <summary>
    /// No usable bootstrap configuration has been persisted.
    /// </summary>
public const string Setup = "Setup";
    /// <summary>
    /// Bootstrap configuration is persisted and runtime seeding is pending.
    /// </summary>
public const string Configured = "Configured";
    /// <summary>
    /// Runtime setup and seeding completed successfully.
    /// </summary>
public const string Running = "Running";
    /// <summary>
    /// Runtime bootstrap did not complete successfully.
    /// </summary>
public const string Failed = "Failed";
}

/// <summary>
/// Represents the effective setup lifecycle and persisted bootstrap selections.
/// </summary>
public sealed class BootstrapState
{
    /// <summary>
    /// Gets or sets the persisted lifecycle value.
    /// </summary>
public string State { get; set; } = BootstrapStates.Setup;
    /// <summary>
    /// Gets or sets whether the overall setup workflow completed.
    /// </summary>
public bool SetupComplete { get; set; }
    /// <summary>
    /// Gets or sets whether initial data seeding completed.
    /// </summary>
public bool SeedComplete { get; set; }
    /// <summary>
    /// Gets or sets the configured database deployment mode.
    /// </summary>
public string? DatabaseMode { get; set; }
    /// <summary>
    /// Gets or sets the configured cache deployment mode.
    /// </summary>
public string? CacheMode { get; set; }
    /// <summary>
    /// Gets or sets the selected secret provider.
    /// </summary>
public string? SecretProvider { get; set; }
    /// <summary>
    /// Gets or sets the selected authentication mode.
    /// </summary>
public string? AuthenticationMode { get; set; }
    /// <summary>
    /// Gets or sets whether bootstrap configuration is available.
    /// </summary>
public bool HasBootstrapConfig { get; set; }

    /// <summary>
    /// Gets whether <see cref="State"/> identifies the setup state, ignoring case.
    /// </summary>
public bool IsSetupMode => string.Equals(State, BootstrapStates.Setup, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether <see cref="State"/> identifies the configured state, ignoring case.
    /// </summary>
public bool IsConfiguredMode => string.Equals(State, BootstrapStates.Configured, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether <see cref="State"/> identifies the running state, ignoring case.
    /// </summary>
public bool IsRunningMode => string.Equals(State, BootstrapStates.Running, StringComparison.OrdinalIgnoreCase);
}
