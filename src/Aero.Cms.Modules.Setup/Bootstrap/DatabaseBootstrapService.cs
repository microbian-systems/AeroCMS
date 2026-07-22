using System.Text.Json;
using System.Text.Json.Nodes;
using Aero.AppServer;
using Aero.Cms.Modules.Setup.Configuration;
using Aero.Secrets;
using Aero.Secrets.Models;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Persists database bootstrap configuration while routing credentials through the selected secret provider.
/// </summary>
/// <remarks>
/// Embedded mode removes the bootstrap connection-string, username, and password reference
/// keys because the runtime derives its data path during dependency registration. It does not
/// remove an existing <c>ConnectionStrings:aero</c> value. Server-mode secrets are stored
/// through either the local secret manager or Infisical. Infisical references are retained in
/// bootstrap configuration without copying the remote value into <c>ConnectionStrings</c>.
/// </remarks>
public sealed class DatabaseBootstrapService(
    IEnvironmentAppSettingsWriter appSettingsWriter,
    ISecretManager secretManager,
    IOptionsMonitor<AeroDbOptions> embeddedOptions,
    InfisicalBootstrapSettingsProvider infisicalSettingsProvider) : IDatabaseBootstrapService
{
    /// <inheritdoc />
public async Task PersistAsync(DatabaseBootstrapModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var root = await ReadOrCreateAsync(env, cancellationToken);
        var bootstrap = GetOrCreateObject(root, "AeroCms", "Bootstrap");

        bootstrap["State"] = BootstrapStates.Configured;
        bootstrap["DatabaseMode"] = model.DatabaseMode;
        bootstrap["SecretProvider"] = model.SecretProvider;
        bootstrap["RequestedManagerAuthenticationProvider"] = model.RequestedManagerAuthenticationProvider;
        bootstrap["RequestedMemberAuthenticationProvider"] = model.RequestedMemberAuthenticationProvider;
        bootstrap.Remove("AuthenticationMode");
        bootstrap["DatabaseUnauthenticated"] = model.DatabaseUnauthenticated;
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
            bootstrap.Remove("DatabaseUsernameReference");
            bootstrap.Remove("DatabasePasswordReference");
            // Sable embedded (SurrealDB KV) requires no connection string.
            // The data path is derived from env.ContentRootPath at DI registration time.
        }
        else if (!string.IsNullOrWhiteSpace(model.ConnectionString) && model.DatabaseMode.Equals("Server", StringComparison.OrdinalIgnoreCase))
        {
            var stored = StoreDatabaseSecret(model.ConnectionString, "AeroCms:Database:ConnectionString", model);
            bootstrap["DatabaseConnectionStringReference"] = stored.Metadata ?? stored.Value;
            if (ShouldStoreEncryptedValue(model.SecretProvider))
            {
                SetConnectionString(root, "aero", stored);
            }

            if (model.DatabaseUnauthenticated)
            {
                bootstrap.Remove("DatabaseUsernameReference");
                bootstrap.Remove("DatabasePasswordReference");
            }
            else
            {
                var username = StoreDatabaseSecret(model.DatabaseUsername ?? string.Empty, "AeroCms:Database:Username", model);
                var password = StoreDatabaseSecret(model.DatabasePassword ?? string.Empty, "AeroCms:Database:Password", model);
                bootstrap["DatabaseUsernameReference"] = username.Metadata ?? username.Value;
                bootstrap["DatabasePasswordReference"] = password.Metadata ?? password.Value;
            }
        }

        await appSettingsWriter.WriteAsync(env,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    /// <summary>
    /// Stores a database secret with the provider selected by the setup request.
    /// </summary>
    private StoredSecretReference StoreDatabaseSecret(string value, string name, DatabaseBootstrapModel model)
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
            return manager.Store(value, name, SecretProviderType.Infisical);
        }

        return secretManager.Store(value, name, SecretProviderType.Local);
    }

    /// <summary>
    /// Protects Infisical bootstrap credentials locally so the external provider can be contacted after restart.
    /// </summary>
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

    /// <summary>
    /// Determines whether a locally protected value must also be placed in the conventional connection-string section.
    /// </summary>
    private static bool ShouldStoreEncryptedValue(string secretProvider)
        => !secretProvider.Equals("Infisical", StringComparison.OrdinalIgnoreCase);

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
    /// Copies the locally protected representation of a stored secret into the connection-string section.
    /// </summary>
    private static void SetConnectionString(JsonNode root, string key, StoredSecretReference reference)
        => SetConnectionString(root, key, reference.Value ?? string.Empty);

    /// <summary>
    /// Sets a named value in the connection-string section while preserving unrelated settings.
    /// </summary>
    private static void SetConnectionString(JsonNode root, string key, string value)
        => GetOrCreateObject(root, "ConnectionStrings")[key] = value;

    /// <summary>
    /// Reads the environment settings object or creates an empty root when the file is absent.
    /// </summary>
    private static async Task<JsonObject> ReadOrCreateAsync(string env, CancellationToken cancellationToken)
    {
        var path = AppSettingsPathResolver.GetAppSettingsFilePath(env);
        if (!File.Exists(path)) return new JsonObject();

        var text = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
    }
}
