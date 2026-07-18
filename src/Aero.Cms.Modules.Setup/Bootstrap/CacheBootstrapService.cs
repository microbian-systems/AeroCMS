using System.Text.Json;
using System.Text.Json.Nodes;
using Aero.AppServer;
using Aero.Cms.Modules.Setup.Configuration;
using Aero.Secrets;
using Aero.Secrets.Models;

namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Represents a class for CacheBootstrapService.
/// </summary>
public sealed class CacheBootstrapService(
    IEnvironmentAppSettingsWriter appSettingsWriter,
    ISecretManager secretManager,
    InfisicalBootstrapSettingsProvider infisicalSettingsProvider) : ICacheBootstrapService
{
        /// <summary>
    /// PersistAsync method.
    /// </summary>
    public async Task PersistAsync(CacheBootstrapModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!model.CacheMode.Equals(AeroAppServerConstants.LocalCacheMode, StringComparison.OrdinalIgnoreCase)
            && !model.CacheMode.Equals(AeroAppServerConstants.ServerCacheMode, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentOutOfRangeException(
                nameof(model),
                model.CacheMode,
                $"Cache mode must be '{AeroAppServerConstants.LocalCacheMode}' or '{AeroAppServerConstants.ServerCacheMode}'.");
        }
        if (model.CacheMode.Equals(AeroAppServerConstants.ServerCacheMode, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(model.ConnectionString))
        {
            throw new ArgumentException(
                "A remote cache connection string is required when cache mode is Server.",
                nameof(model));
        }

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var root = await ReadOrCreateAsync(env, cancellationToken);
        var bootstrap = GetOrCreateObject(root, "AeroCms", "Bootstrap");

        bootstrap["State"] = BootstrapStates.Configured;
        bootstrap["CacheMode"] = model.CacheMode;
        bootstrap["SecretProvider"] = model.SecretProvider;
        bootstrap["HasBootstrapConfig"] = model.HasBootstrapConfig;
        bootstrap["SetupComplete"] = false;
        bootstrap["SeedComplete"] = false;
        bootstrap.Remove("CacheConnectionStringReference");
        bootstrap.Remove("CacheConnectionString");
        GetOrCreateObject(root, "ConnectionStrings").Remove("cache");

        if (model.SecretProvider.Equals("Infisical", StringComparison.OrdinalIgnoreCase))
        {
            PersistInfisicalAuth(bootstrap, model);
        }

        if (!string.IsNullOrWhiteSpace(model.ConnectionString) && model.CacheMode.Equals("Server", StringComparison.OrdinalIgnoreCase))
        {
            var stored = StoreConnectionString(model.ConnectionString, "AeroCms:Cache:ConnectionString", model);
            bootstrap["CacheConnectionStringReference"] = stored.Metadata ?? stored.Value;
            if (!model.SecretProvider.Equals("Infisical", StringComparison.OrdinalIgnoreCase))
            {
                GetOrCreateObject(root, "ConnectionStrings")["cache"] = stored.Value;
            }
        }

        await appSettingsWriter.WriteAsync(env,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    private StoredSecretReference StoreConnectionString(string connectionString, string name, CacheBootstrapModel model)
    {
        if (model.SecretProvider.Equals("Infisical", StringComparison.OrdinalIgnoreCase))
        {
            var infisicalSettings = infisicalSettingsProvider.GetSettings();
            var infisical = infisicalSettings with
            {
                MachineId = string.IsNullOrWhiteSpace(model.InfisicalMachineId) ? infisicalSettings.MachineId : model.InfisicalMachineId,
                ClientSecret = string.IsNullOrWhiteSpace(model.InfisicalClientSecret) ? infisicalSettings.ClientSecret : model.InfisicalClientSecret
            };
            var manager = new InfisicalSecretManager(infisical);
            return manager.Store(connectionString, name, SecretProviderType.Infisical);
        }

        return secretManager.Store(connectionString, name, SecretProviderType.Local);
    }

    private void PersistInfisicalAuth(JsonObject bootstrap, CacheBootstrapModel model)
    {
        var infisicalSettings = infisicalSettingsProvider.GetSettings();
        var machineId = string.IsNullOrWhiteSpace(model.InfisicalMachineId) ? infisicalSettings.MachineId : model.InfisicalMachineId;
        var clientSecret = string.IsNullOrWhiteSpace(model.InfisicalClientSecret) ? infisicalSettings.ClientSecret : model.InfisicalClientSecret;

        if (!string.IsNullOrWhiteSpace(machineId))
        {
            var storedMachineId = secretManager.Store(machineId, "AeroCms:Bootstrap:Infisical:MachineId");
            bootstrap["InfisicalMachineId"] = storedMachineId.Value;
            bootstrap["InfisicalMachineIdReference"] = storedMachineId.Metadata ?? storedMachineId.Value;
        }

        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            var storedClientSecret = secretManager.Store(clientSecret, "AeroCms:Bootstrap:Infisical:ClientSecret");
            bootstrap["InfisicalClientSecret"] = storedClientSecret.Value;
            bootstrap["InfisicalClientSecretReference"] = storedClientSecret.Metadata ?? storedClientSecret.Value;
        }
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

    private static async Task<JsonObject> ReadOrCreateAsync(string env, CancellationToken cancellationToken)
    {
        var path = AppSettingsPathResolver.GetAppSettingsFilePath(env);
        if (!File.Exists(path)) return new JsonObject();

        var text = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
    }
}
