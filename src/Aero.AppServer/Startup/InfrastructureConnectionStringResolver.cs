using Aero.Secrets;
using Aero.Secrets.Models;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aero.AppServer.Startup;

/// <summary>
/// Contains the effective database, cache, and secret-provider settings selected during bootstrap.
/// </summary>
/// <param name="DatabaseConnectionString">The embedded URI or server endpoint used by AeroDB.</param>
/// <param name="CacheConnectionString">The Redis-compatible endpoint, or <see langword="null"/> when absent.</param>
/// <param name="DatabaseMode">The configured database mode.</param>
/// <param name="CacheMode">The validated local or server cache mode.</param>
/// <param name="SecretProvider">The provider used to resolve protected configuration.</param>
/// <param name="DatabaseUsername">The optional server-mode database user name.</param>
/// <param name="DatabasePassword">The optional server-mode database password.</param>
/// <param name="DatabaseUnauthenticated">Whether server-mode database authentication is intentionally disabled.</param>
public sealed record ResolvedInfrastructureSettings(
    string DatabaseConnectionString,
    string? CacheConnectionString,
    string DatabaseMode,
    string CacheMode,
    string SecretProvider,
    string? DatabaseUsername = null,
    string? DatabasePassword = null,
    bool DatabaseUnauthenticated = false);

/// <summary>
/// Resolves bootstrap configuration and protected secret references into runtime infrastructure settings.
/// </summary>
/// <param name="configuration">The application configuration, including bootstrap and provider settings.</param>
public sealed class InfrastructureConnectionStringResolver(IConfiguration configuration)
{
    /// <summary>
    /// Resolves the database and cache endpoints required by the current bootstrap state.
    /// </summary>
    /// <returns>The effective infrastructure settings.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown for an unsupported cache mode, a missing server secret reference, missing Infisical
    /// bootstrap credentials, or an error reported by the selected secret provider.
    /// </exception>
    /// <remarks>
    /// Setup state always selects the embedded database so the host can start before configuration
    /// is complete. Local plaintext connection values can be replaced in the environment-specific
    /// appsettings file with newly stored secret references as a side effect of resolution.
    /// </remarks>
public ResolvedInfrastructureSettings Resolve()
    {
        var embedded = AeroDbOptions.FromConfiguration(configuration);
        var bootstrap = configuration.GetSection("AeroCms:Bootstrap");
        var hasBootstrap = bootstrap.GetValue<bool?>("HasBootstrapConfig")
            ?? (bootstrap.Exists() && !string.IsNullOrWhiteSpace(bootstrap["DatabaseMode"]));
        var state = bootstrap["State"];
        if (string.IsNullOrWhiteSpace(state))
        {
            var setupComplete = bootstrap.GetValue<bool?>("SetupComplete") ?? false;
            var seedComplete = bootstrap.GetValue<bool?>("SeedComplete") ?? false;
            state = setupComplete && seedComplete
                ? "Running"
                : hasBootstrap
                    ? "Configured"
                    : "Setup";
        }

        // In Setup mode, return embedded defaults so the main app can still boot its hosted infra safely.
        var isSetupMode = string.Equals(state, "Setup", StringComparison.OrdinalIgnoreCase);

        var databaseMode = bootstrap["DatabaseMode"] ?? "Embedded";
        var cacheMode = bootstrap["CacheMode"] ?? AeroAppServerConstants.LocalCacheMode;
        var secretProvider = bootstrap["SecretProvider"] ?? "Local Certificate";
        ValidateCacheMode(cacheMode);

        if (isSetupMode)
        {
            var cacheConn = cacheMode.Equals(AeroAppServerConstants.LocalCacheMode, StringComparison.OrdinalIgnoreCase)
                ? AeroAppServerConstants.CacheUrl
                : null;
            return new ResolvedInfrastructureSettings(
                embedded.ConnectionString,
                cacheConn,
                "Embedded",
                cacheMode,
                "Local Certificate");
        }

        var secretManager = DataProtectionCertificateBootstrapper.CreateSecretManager(configuration);
        var db = ResolveDatabase(databaseMode, secretProvider, bootstrap, hasBootstrap, secretManager);
        var cache = ResolveCache(cacheMode, secretProvider, bootstrap, hasBootstrap, secretManager);
        var databaseUnauthenticated = bootstrap.GetValue<bool?>("DatabaseUnauthenticated") ?? false;
        var credentials = ResolveDatabaseCredentials(databaseMode, databaseUnauthenticated, secretProvider, bootstrap, secretManager);
        return new ResolvedInfrastructureSettings(db, cache, databaseMode, cacheMode, secretProvider,
            credentials.username, credentials.password, databaseUnauthenticated);
    }

    /// <summary>
    /// Resolves an embedded database URI or the configured server database secret.
    /// </summary>
    /// <returns>The effective database connection value.</returns>
    private string ResolveDatabase(string databaseMode, string secretProvider, IConfigurationSection bootstrap, bool hasBootstrap, ISecretManager secretManager)
    {
        if (!hasBootstrap || databaseMode.Equals("Embedded", StringComparison.OrdinalIgnoreCase))
            return AeroDbOptions.FromConfiguration(configuration).ConnectionString;

        return ResolveServerValue("aero", "DatabaseConnectionStringReference", bootstrap, secretProvider, "database", secretManager);
    }

    /// <summary>
    /// Resolves the local Garnet endpoint or the configured server cache secret.
    /// </summary>
    /// <returns>The effective cache endpoint.</returns>
    private string? ResolveCache(string cacheMode, string secretProvider, IConfigurationSection bootstrap, bool hasBootstrap, ISecretManager secretManager)
    {
        if (cacheMode.Equals(AeroAppServerConstants.LocalCacheMode, StringComparison.OrdinalIgnoreCase))
            return AeroAppServerConstants.CacheUrl;

        return ResolveServerValue("cache", "CacheConnectionStringReference", bootstrap, secretProvider, "cache", secretManager);
    }

    /// <summary>
    /// Rejects cache modes that cannot be represented by the application-server registrations.
    /// </summary>
    /// <param name="cacheMode">The configured cache-mode value.</param>
    /// <exception cref="InvalidOperationException">Thrown when the value is neither local nor server mode.</exception>
    private static void ValidateCacheMode(string cacheMode)
    {
        if (cacheMode.Equals(AeroAppServerConstants.LocalCacheMode, StringComparison.OrdinalIgnoreCase)
            || cacheMode.Equals(AeroAppServerConstants.ServerCacheMode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Unsupported cache mode '{cacheMode}'. Expected '{AeroAppServerConstants.LocalCacheMode}' or '{AeroAppServerConstants.ServerCacheMode}'.");
    }

    /// <summary>
    /// Resolves optional database credentials for authenticated server mode.
    /// </summary>
    /// <returns>Resolved credentials, or a pair of <see langword="null"/> values when they are not required.</returns>
    private (string? username, string? password) ResolveDatabaseCredentials(
        string databaseMode,
        bool databaseUnauthenticated,
        string secretProvider,
        IConfigurationSection bootstrap,
        ISecretManager secretManager)
    {
        if (databaseMode.Equals("Embedded", StringComparison.OrdinalIgnoreCase) || databaseUnauthenticated)
        {
            return (null, null);
        }

        return (
            ResolveOptionalServerValue("AeroCms:Database:Username", "DatabaseUsernameReference", bootstrap, secretProvider, secretManager),
            ResolveOptionalServerValue("AeroCms:Database:Password", "DatabasePasswordReference", bootstrap, secretProvider, secretManager));
    }

    /// <summary>
    /// Reads an optional local or Infisical secret referenced by bootstrap configuration.
    /// </summary>
    /// <returns>The resolved value, or <see langword="null"/> when no reference is configured.</returns>
    private string? ResolveOptionalServerValue(
        string secretName,
        string referenceKey,
        IConfigurationSection bootstrap,
        string secretProvider,
        ISecretManager secretManager)
    {
        var reference = bootstrap[referenceKey];
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        if (secretProvider.Equals("Infisical", StringComparison.OrdinalIgnoreCase))
        {
            var auth = ReadProtectedBootstrapAuth(bootstrap, secretManager);
            return CreateInfisicalManager(auth.machineId, auth.clientSecret)
                .Read(new StoredSecretReference(SecretProviderType.Infisical, secretName, null, reference));
        }

        return secretManager.Read(new StoredSecretReference(SecretProviderType.Local, secretName, reference));
    }

    /// <summary>
    /// Resolves a required server endpoint from the selected secret provider.
    /// </summary>
    /// <returns>The resolved server value.</returns>
    /// <remarks>
    /// Local-certificate values that resolve to the same plaintext stored in configuration are
    /// opportunistically upgraded to secret references.
    /// </remarks>
    private string ResolveServerValue(string connectionName, string referenceKey, IConfigurationSection bootstrap, string secretProvider, string label, ISecretManager secretManager)
    {
        var reference = bootstrap[referenceKey] ?? bootstrap[$"{label}ConnectionStringReference"];
        if (string.IsNullOrWhiteSpace(reference))
            throw new InvalidOperationException($"Bootstrap is configured for server {label} mode but no secret reference was stored.");

        if (secretProvider.Equals("Infisical", StringComparison.OrdinalIgnoreCase))
        {
            var auth = ReadProtectedBootstrapAuth(bootstrap, secretManager);
            var infisical = CreateInfisicalManager(auth.machineId, auth.clientSecret);
            return infisical.Read(new StoredSecretReference(SecretProviderType.Infisical, connectionName, null, reference));
        }

        var resolved = secretManager.Read(new StoredSecretReference(SecretProviderType.Local, connectionName, reference));
        TryUpgradePlaintextLocalSecret(referenceKey, connectionName, reference, resolved, secretManager);
        return resolved;
    }

    /// <summary>
    /// Replaces a legacy plaintext local connection value with a protected reference when possible.
    /// </summary>
    /// <remarks>
    /// Missing or unparsable appsettings files are ignored. Successful upgrades rewrite the full
    /// environment-specific JSON document with indentation and update both bootstrap and
    /// <c>ConnectionStrings</c> entries.
    /// </remarks>
    private void TryUpgradePlaintextLocalSecret(string referenceKey, string connectionName, string storedValue, string resolvedValue, ISecretManager secretManager)
    {
        if (string.IsNullOrWhiteSpace(storedValue) || string.Equals(storedValue, resolvedValue, StringComparison.Ordinal) is false)
        {
            return;
        }

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var path = ResolveAppSettingsPath(env);
        if (!File.Exists(path))
        {
            return;
        }

        var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject();
        if (root is null)
        {
            return;
        }

        var stored = secretManager.Store(resolvedValue, $"ConnectionStrings:{connectionName}");
        var bootstrap = GetOrCreateObject(root, "AeroCms", "Bootstrap");
        bootstrap[referenceKey] = stored.Value;
        GetOrCreateObject(root, "ConnectionStrings")[connectionName] = stored.Value;
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Creates an Infisical client from static configuration and protected machine credentials.
    /// </summary>
    /// <returns>A configured secret manager; connectivity is deferred to secret operations.</returns>
    private InfisicalSecretManager CreateInfisicalManager(string machineId, string clientSecret)
    {
        var host = configuration["Infisical:HostUri"] ?? "http://localhost:8080";
        return new InfisicalSecretManager(new InfisicalSecretManagerOptions
        {
            HostUri = Uri.TryCreate(host, UriKind.Absolute, out var uri) ? uri : new Uri("http://localhost:8080"),
            ProjectId = configuration["Infisical:ProjectId"] ?? string.Empty,
            EnvironmentSlug = configuration["Infisical:EnvironmentSlug"] ?? string.Empty,
            SecretPath = configuration["Infisical:SecretPath"] ?? "/",
            MachineId = machineId,
            ClientSecret = clientSecret
        });
    }

    /// <summary>
    /// Resolves Infisical machine credentials from locally protected bootstrap references.
    /// </summary>
    /// <returns>The decrypted machine identifier and client secret.</returns>
    /// <exception cref="InvalidOperationException">Thrown when either protected reference is missing.</exception>
    private (string machineId, string clientSecret) ReadProtectedBootstrapAuth(IConfigurationSection bootstrap, ISecretManager secretManager)
    {
        var machineRef = bootstrap["InfisicalMachineIdReference"];
        var clientRef = bootstrap["InfisicalClientSecretReference"];
        if (string.IsNullOrWhiteSpace(machineRef) || string.IsNullOrWhiteSpace(clientRef))
            throw new InvalidOperationException("Bootstrap is configured for Infisical but encrypted auth material is missing.");

        return (secretManager.Read(new StoredSecretReference(SecretProviderType.Local, "AeroCms:Bootstrap:Infisical:MachineId", machineRef)),
            secretManager.Read(new StoredSecretReference(SecretProviderType.Local, "AeroCms:Bootstrap:Infisical:ClientSecret", clientRef)));
    }

    /// <summary>
    /// Locates the environment-specific appsettings file used for plaintext-secret upgrades.
    /// </summary>
    /// <param name="environmentName">The ASP.NET Core environment name.</param>
    /// <returns>A path under <c>src/Aero.Cms.Web</c> when available, otherwise under the current directory.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="environmentName"/> is empty or whitespace.
    /// </exception>
    private static string ResolveAppSettingsPath(string environmentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        var webProjectPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "Aero.Cms.Web"));
        if (!Directory.Exists(webProjectPath))
        {
            webProjectPath = Directory.GetCurrentDirectory();
        }

        return Path.Combine(webProjectPath, $"appsettings.{environmentName}.json");
    }

    /// <summary>
    /// Traverses or creates a sequence of JSON objects below the supplied root.
    /// </summary>
    /// <param name="root">The root JSON node.</param>
    /// <param name="path">The property path to create.</param>
    /// <returns>The object at the final path segment.</returns>
    private static JsonObject GetOrCreateObject(JsonNode root, params string[] path)
    {
        JsonNode current = root;
        foreach (var segment in path)
        {
            var next = current[segment] as JsonObject ?? new JsonObject();
            current[segment] = next;
            current = next;
        }

        return (JsonObject)current;
    }
}
