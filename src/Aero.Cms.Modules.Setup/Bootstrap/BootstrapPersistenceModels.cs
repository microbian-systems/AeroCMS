namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Represents a record for DatabaseBootstrapModel.
/// </summary>
public sealed record DatabaseBootstrapModel(
    string DatabaseMode,
    string? ConnectionString,
    string SecretProvider,
    string AuthenticationMode,
    string? InfisicalMachineId = null,
    string? InfisicalClientSecret = null,
    string? InfisicalMachineIdReference = null,
    string? InfisicalClientSecretReference = null,
    string? ConnectionStringReference = null,
    bool HasBootstrapConfig = true,
    bool DatabaseUnauthenticated = false,
    string? DatabaseUsername = null,
    string? DatabasePassword = null);

/// <summary>
/// Represents a record for CacheBootstrapModel.
/// </summary>
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
