using System;
using System.IO;
using Aero.Secrets;
using Aero.Secrets.Models;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography.X509Certificates;

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
        var bootstrap = configuration.GetSection("AeroCms:Bootstrap");
        var hasBootstrap = bootstrap.Exists();
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

        // In Setup mode, return embedded defaults to allow the app to start
        // This enables in-process runtime activation after setup configuration
        var isSetupMode = string.Equals(state, "Setup", StringComparison.OrdinalIgnoreCase);

        var databaseMode = bootstrap["DatabaseMode"] ?? "Embedded";
        var cacheMode = bootstrap["CacheMode"] ?? "Memory";
        var secretProvider = bootstrap["SecretProvider"] ?? "Local Certificate";

        // If in Setup mode, always use embedded defaults
        if (isSetupMode)
        {
            var cacheConn = cacheMode.Equals("Memory", StringComparison.OrdinalIgnoreCase)
                ? null
                : AeroAppServerConstants.CacheUrl;
            return new ResolvedInfrastructureSettings(
                AeroAppServerConstants.EmbedConnString,
                cacheConn,
                "Embedded",
                cacheMode,
                "Local Certificate");
        }

        var secretManager = CreateSecretManager();
        var db = ResolveDatabase(databaseMode, secretProvider, bootstrap, hasBootstrap, secretManager);
        var cache = ResolveCache(cacheMode, secretProvider, bootstrap, hasBootstrap, secretManager);
        return new ResolvedInfrastructureSettings(db, cache, databaseMode, cacheMode, secretProvider);
    }

    private string ResolveDatabase(string databaseMode, string secretProvider, IConfigurationSection bootstrap, bool hasBootstrap, ISecretManager secretManager)
    {
        if (!hasBootstrap || databaseMode.Equals("Embedded", StringComparison.OrdinalIgnoreCase))
            return AeroAppServerConstants.EmbedConnString;

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

        return secretManager.Read(new StoredSecretReference(SecretProviderType.Local, connectionName, reference));
    }

    private ISecretManager CreateSecretManager()
    {
        var certPath = configuration["DataProtection:CertificatePath"];
        var certPassword = configuration["DataProtection:CertificatePassword"];
        if (!string.IsNullOrWhiteSpace(certPath) && File.Exists(certPath))
        {
            var cert = string.IsNullOrWhiteSpace(certPassword)
                ? X509CertificateLoader.LoadPkcs12FromFile(certPath, string.Empty, X509KeyStorageFlags.DefaultKeySet)
                : X509CertificateLoader.LoadPkcs12FromFile(certPath, certPassword, X509KeyStorageFlags.DefaultKeySet);
            return new DataProtectionCertificateSecretManager(cert);
        }

        return new LocalSecretManager();
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
}
