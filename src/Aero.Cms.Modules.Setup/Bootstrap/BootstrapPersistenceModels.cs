namespace Aero.Cms.Modules.Setup.Bootstrap;

public sealed record DatabaseBootstrapModel(
    string DatabaseMode,
    string? ConnectionString,
    string SecretProvider,
    string? InfisicalMachineId = null,
    string? InfisicalClientSecret = null,
    string? InfisicalMachineIdReference = null,
    string? InfisicalClientSecretReference = null,
    string? ConnectionStringReference = null,
    bool HasBootstrapConfig = true);

public sealed record CacheBootstrapModel(
    string CacheMode,
    string? ConnectionString,
    string SecretProvider,
    string? InfisicalMachineId = null,
    string? InfisicalClientSecret = null,
    string? InfisicalMachineIdReference = null,
    string? InfisicalClientSecretReference = null,
    string? ConnectionStringReference = null,
    bool HasBootstrapConfig = true);
