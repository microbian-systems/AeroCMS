using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aero.AppServer;
using Aero.Cms.Modules.Setup.Configuration;
using Aero.Secrets;
using Aero.Secrets.Models;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Modules.Setup.Bootstrap;

public sealed class DatabaseBootstrapService(
    IEnvironmentAppSettingsWriter appSettingsWriter,
    ISecretManager secretManager,
    IOptionsMonitor<AeroDbOptions> embeddedOptions,
    InfisicalBootstrapSettingsProvider infisicalSettingsProvider) : IDatabaseBootstrapService
{
    public async Task PersistAsync(DatabaseBootstrapModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var root = await ReadOrCreateAsync(env, cancellationToken);
        var bootstrap = GetOrCreateObject(root, "AeroCms", "Bootstrap");

        bootstrap["State"] = BootstrapStates.Configured;
        bootstrap["DatabaseMode"] = model.DatabaseMode;
        bootstrap["SecretProvider"] = model.SecretProvider;
        bootstrap["AuthenticationMode"] = model.AuthenticationMode;
        bootstrap["HasBootstrapConfig"] = model.HasBootstrapConfig;
        bootstrap["SetupComplete"] = false;
        bootstrap["SeedComplete"] = false;

        if (model.SecretProvider.Equals("Infisical", StringComparison.OrdinalIgnoreCase))
        {
            PersistInfisicalAuth(bootstrap, model);
        }

        if (model.DatabaseMode.Equals("Embedded", StringComparison.OrdinalIgnoreCase))
        {
            bootstrap.Remove("DatabaseConnectionStringReference");
            SetConnectionString(root, "aero", embeddedOptions.CurrentValue.ConnectionString);
        }
        else if (!string.IsNullOrWhiteSpace(model.ConnectionString) && model.DatabaseMode.Equals("Server", StringComparison.OrdinalIgnoreCase))
        {
            var stored = StoreConnectionString(model.ConnectionString, "AeroCms:Database:ConnectionString", model);
            bootstrap["DatabaseConnectionStringReference"] = stored.Metadata ?? stored.Value;
            if (ShouldStoreEncryptedValue(model.SecretProvider))
            {
                SetConnectionString(root, "aero", stored);
            }
        }

        await appSettingsWriter.WriteAsync(env,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    private StoredSecretReference StoreConnectionString(string connectionString, string name, DatabaseBootstrapModel model)
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

    private void PersistInfisicalAuth(JsonObject bootstrap, DatabaseBootstrapModel model)
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

    private static bool ShouldStoreEncryptedValue(string secretProvider)
        => !secretProvider.Equals("Infisical", StringComparison.OrdinalIgnoreCase);

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

    private static void SetConnectionString(JsonNode root, string key, StoredSecretReference reference)
        => SetConnectionString(root, key, reference.Value ?? string.Empty);

    private static void SetConnectionString(JsonNode root, string key, string value)
        => GetOrCreateObject(root, "ConnectionStrings")[key] = value;

    private static async Task<JsonObject> ReadOrCreateAsync(string env, CancellationToken cancellationToken)
    {
        var path = AppSettingsPathResolver.GetAppSettingsFilePath(env);
        if (!File.Exists(path)) return new JsonObject();

        var text = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
    }
}
