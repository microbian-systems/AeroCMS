using System;
using System.IO;
using Aero.Secrets;
using Aero.Secrets.Models;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aero.AppServer.Startup;

public sealed record ResolvedInfrastructureSettings(
    string DatabaseConnectionString,
    string? CacheConnectionString,
    string DatabaseMode,
    string CacheMode,
    string SecretProvider);

public sealed class InfrastructureConnectionStringResolver(IConfiguration configuration)
{
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
        var cacheMode = bootstrap["CacheMode"] ?? "Memory";
        var secretProvider = bootstrap["SecretProvider"] ?? "Local Certificate";

        if (isSetupMode)
        {
            var cacheConn = cacheMode.Equals("Memory", StringComparison.OrdinalIgnoreCase)
                ? null
                : AeroAppServerConstants.CacheUrl;
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
        return new ResolvedInfrastructureSettings(db, cache, databaseMode, cacheMode, secretProvider);
    }

    private string ResolveDatabase(string databaseMode, string secretProvider, IConfigurationSection bootstrap, bool hasBootstrap, ISecretManager secretManager)
    {
        if (!hasBootstrap || databaseMode.Equals("Embedded", StringComparison.OrdinalIgnoreCase))
            return AeroDbOptions.FromConfiguration(configuration).ConnectionString;

        return ResolveServerValue("aero", "DatabaseConnectionStringReference", bootstrap, secretProvider, "database", secretManager);
    }

    private string? ResolveCache(string cacheMode, string secretProvider, IConfigurationSection bootstrap, bool hasBootstrap, ISecretManager secretManager)
    {
        if (cacheMode.Equals("Memory", StringComparison.OrdinalIgnoreCase))
            return null;

        if (cacheMode.Equals("Embedded", StringComparison.OrdinalIgnoreCase))
            return AeroAppServerConstants.CacheUrl;

        return ResolveServerValue("cache", "CacheConnectionStringReference", bootstrap, secretProvider, "cache", secretManager);
    }

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

    private (string machineId, string clientSecret) ReadProtectedBootstrapAuth(IConfigurationSection bootstrap, ISecretManager secretManager)
    {
        var machineRef = bootstrap["InfisicalMachineIdReference"];
        var clientRef = bootstrap["InfisicalClientSecretReference"];
        if (string.IsNullOrWhiteSpace(machineRef) || string.IsNullOrWhiteSpace(clientRef))
            throw new InvalidOperationException("Bootstrap is configured for Infisical but encrypted auth material is missing.");

        return (secretManager.Read(new StoredSecretReference(SecretProviderType.Local, "AeroCms:Bootstrap:Infisical:MachineId", machineRef)),
            secretManager.Read(new StoredSecretReference(SecretProviderType.Local, "AeroCms:Bootstrap:Infisical:ClientSecret", clientRef)));
    }

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
