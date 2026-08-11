namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Describes database and secret-provider values to persist before runtime initialization.
/// </summary>
/// <remarks>
/// Credential and connection-string values may contain plaintext supplied by the setup UI.
/// Persistence services are responsible for protecting them before writing configuration.
/// Reference properties allow a caller to carry provider-specific secret identifiers.
/// </remarks>
public sealed record DatabaseBootstrapModel(
    string DatabaseMode,
    string? ConnectionString,
    string SecretProvider,
    string RequestedManagerAuthenticationProvider,
    string RequestedMemberAuthenticationProvider,
    string? InfisicalMachineId = null,
    string? InfisicalClientSecret = null,
    string? InfisicalMachineIdReference = null,
    string? InfisicalClientSecretReference = null,
    string? ConnectionStringReference = null,
    bool HasBootstrapConfig = true,
    bool DatabaseUnauthenticated = false,
    string? DatabaseUsername = null,
    string? DatabasePassword = null)
{
    /// <summary>Gets the installation-wide SurrealDB namespace.</summary>
    public string DatabaseNamespace { get; init; } = Aero.AppServer.AeroAppServerConstants.SableNamespace;

    /// <summary>Gets the installation-wide SurrealDB database name.</summary>
    public string DatabaseName { get; init; } = Aero.AppServer.AeroAppServerConstants.SableDatabase;
}

/// <summary>
/// Describes cache and secret-provider values to persist before runtime initialization.
/// </summary>
/// <remarks>
/// A connection string is required by <see cref="ICacheBootstrapService"/> for server mode.
/// Sensitive values are inputs to secret storage and must not be logged or written as plaintext.
/// </remarks>
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
