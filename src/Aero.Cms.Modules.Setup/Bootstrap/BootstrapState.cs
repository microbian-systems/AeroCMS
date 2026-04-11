namespace Aero.Cms.Modules.Setup.Bootstrap;

public static class BootstrapStates
{
    public const string Setup = "Setup";
    public const string Configured = "Configured";
    public const string Running = "Running";
    public const string Failed = "Failed";
}

public sealed class BootstrapState
{
    public string State { get; set; } = BootstrapStates.Setup;
    public bool SetupComplete { get; set; }
    public bool SeedComplete { get; set; }
    public string? DatabaseMode { get; set; }
    public string? CacheMode { get; set; }
    public string? SecretProvider { get; set; }
    public bool HasBootstrapConfig { get; set; }

    public bool IsSetupMode => string.Equals(State, BootstrapStates.Setup, StringComparison.OrdinalIgnoreCase);

    public bool IsConfiguredMode => string.Equals(State, BootstrapStates.Configured, StringComparison.OrdinalIgnoreCase);

    public bool IsRunningMode => string.Equals(State, BootstrapStates.Running, StringComparison.OrdinalIgnoreCase);
}
