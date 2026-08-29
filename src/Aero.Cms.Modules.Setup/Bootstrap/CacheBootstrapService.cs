using System.Text.Json;
using System.Text.Json.Nodes;
using Aero.AppServer;
using Aero.AppServer.Startup;
using Aero.Cms.Modules.Setup.Configuration;
using Aero.Secrets;
using Aero.Secrets.Models;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Persists cache bootstrap configuration while routing sensitive values through the selected secret provider.
/// </summary>
/// <remarks>
/// Repeated calls replace the cache-related bootstrap keys and remove stale connection
/// values before writing the new selection. Server mode requires a non-empty connection
/// string; local mode removes the conventional <c>ConnectionStrings:cache</c> value.
/// </remarks>
public sealed class CacheBootstrapService(
    IEnvironmentAppSettingsWriter appSettingsWriter,
    ISecretManager secretManager,
    InfisicalBootstrapSettingsProvider infisicalSettingsProvider,
    IHostEnvironment hostEnvironment) : ICacheBootstrapService
{
    /// <inheritdoc />
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

        var env = hostEnvironment.EnvironmentName;
        var root = await ReadOrCreateAsync(env, cancellationToken);
        var bootstrap = GetOrCreateObject(root, "AeroCms", "Bootstrap");
        var infrastructure = GetOrCreateObject(root, "AeroCms", "Infrastructure");

        bootstrap["State"] = BootstrapStates.Setup;
        bootstrap["HasBootstrapConfig"] = model.HasBootstrapConfig;
        bootstrap["SetupComplete"] = false;
        bootstrap["SeedComplete"] = false;
        infrastructure[AeroCmsInfrastructureConfiguration.CacheMode] = model.CacheMode;
        infrastructure[AeroCmsInfrastructureConfiguration.SecretProvider] = model.SecretProvider;
        infrastructure.Remove("CacheConnectionStringReference");
        infrastructure.Remove("CacheConnectionString");
        GetOrCreateObject(root, "ConnectionStrings").Remove("cache");

        if (model.SecretProvider.Equals("Infisical", StringComparison.OrdinalIgnoreCase))
        {
            PersistInfisicalAuth(infrastructure, model);
        }

        if (!string.IsNullOrWhiteSpace(model.ConnectionString) && model.CacheMode.Equals("Server", StringComparison.OrdinalIgnoreCase))
        {
            var stored = StoreConnectionString(model.ConnectionString, "AeroCms:Cache:ConnectionString", model);
            infrastructure["CacheConnectionStringReference"] = stored.Metadata ?? stored.Value;
            if (!model.SecretProvider.Equals("Infisical", StringComparison.OrdinalIgnoreCase))
            {
                GetOrCreateObject(root, "ConnectionStrings")["cache"] = stored.Value;
            }
        }

        await appSettingsWriter.WriteAsync(env,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    /// <summary>
    /// Stores a cache connection string with the selected secret provider.
    /// </summary>
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

    /// <summary>
    /// Protects Infisical bootstrap credentials locally so the external provider can be contacted after restart.
    /// </summary>
    private void PersistInfisicalAuth(JsonObject infrastructure, CacheBootstrapModel model)
    {
        var infisicalSettings = infisicalSettingsProvider.GetSettings();
        var machineId = string.IsNullOrWhiteSpace(model.InfisicalMachineId) ? infisicalSettings.MachineId : model.InfisicalMachineId;
        var clientSecret = string.IsNullOrWhiteSpace(model.InfisicalClientSecret) ? infisicalSettings.ClientSecret : model.InfisicalClientSecret;

        if (!string.IsNullOrWhiteSpace(machineId))
        {
            var storedMachineId = secretManager.Store(machineId, "AeroCms:Infrastructure:Infisical:MachineId");
            infrastructure["InfisicalMachineId"] = storedMachineId.Value;
            infrastructure["InfisicalMachineIdReference"] = storedMachineId.Metadata ?? storedMachineId.Value;
        }

        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            var storedClientSecret = secretManager.Store(clientSecret, "AeroCms:Infrastructure:Infisical:ClientSecret");
            infrastructure["InfisicalClientSecret"] = storedClientSecret.Value;
            infrastructure["InfisicalClientSecretReference"] = storedClientSecret.Metadata ?? storedClientSecret.Value;
        }
    }

    /// <summary>
    /// Traverses an object path, creating missing JSON objects without replacing sibling settings.
    /// </summary>
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

    /// <summary>
    /// Reads the environment settings object or creates an empty root when the file is absent.
    /// </summary>
    private async Task<JsonObject> ReadOrCreateAsync(string env, CancellationToken cancellationToken)
    {
        var path = appSettingsWriter.GetFilePath(env);
        if (!File.Exists(path)) return new JsonObject();

        var text = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
    }
}
