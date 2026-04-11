namespace Aero.AppServer.Startup;

public interface IInfrastructureReadinessSnapshot
{
    bool SetupComplete { get; set; }
    bool SeedComplete { get; set; }
    bool HasBootstrapConfig { get; set; }
    string? DatabaseMode { get; set; }
    string? CacheMode { get; set; }
    string? SecretProvider { get; set; }
    bool PostgresReady { get; set; }
    bool GarnetReady { get; set; }
}
