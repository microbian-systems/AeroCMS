namespace Aero.AppServer.Startup;

public sealed class InfrastructureReadinessSnapshot : IInfrastructureReadinessSnapshot
{
    public bool SetupComplete { get; set; }
    public bool SeedComplete { get; set; }
    public bool HasBootstrapConfig { get; set; }
    public string? DatabaseMode { get; set; }
    public string? CacheMode { get; set; }
    public string? SecretProvider { get; set; }
    public bool PostgresReady { get; set; }
    public bool GarnetReady { get; set; }
}
