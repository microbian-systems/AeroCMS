using System.Text.Json;
using System.Text.Json.Nodes;
using Aero.Cms.Modules.Setup.Configuration;
using Aero.Secrets;
using Aero.Secrets.Models;

namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Defines an interface for IBootstrapPendingSetupRequestStore.
/// </summary>
public interface IBootstrapPendingSetupRequestStore
{
        /// <summary>
    /// SaveAsync method.
    /// </summary>
Task SaveAsync(SeedDatabaseRequest request, CancellationToken cancellationToken = default);

        /// <summary>
    /// LoadAsync method.
    /// </summary>
Task<SeedDatabaseRequest?> LoadAsync(CancellationToken cancellationToken = default);

        /// <summary>
    /// ClearAsync method.
    /// </summary>
Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a class for BootstrapPendingSetupRequestStore.
/// </summary>
public sealed class BootstrapPendingSetupRequestStore(
    IEnvironmentAppSettingsWriter appSettingsWriter,
    ISecretManager secretManager) : IBootstrapPendingSetupRequestStore
{
    private const string PendingSeedKey = "PendingSeedPayload";
    private const string PendingSeedName = "AeroCms:Bootstrap:PendingSeed";

        /// <summary>
    /// SaveAsync method.
    /// </summary>
public async Task SaveAsync(SeedDatabaseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var root = await ReadOrCreateAsync(env, cancellationToken);
        var bootstrap = GetOrCreateObject(root, "AeroCms", "Bootstrap");
        var payload = JsonSerializer.Serialize(request);
        var stored = secretManager.Store(payload, PendingSeedName, SecretProviderType.Local);
        bootstrap[PendingSeedKey] = stored.Value;

        await appSettingsWriter.WriteAsync(env, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
    }

        /// <summary>
    /// LoadAsync method.
    /// </summary>
public async Task<SeedDatabaseRequest?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var root = await ReadOrCreateAsync(env, cancellationToken);
        var payload = root["AeroCms"]?["Bootstrap"]?[PendingSeedKey]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        var json = secretManager.Read(new StoredSecretReference(SecretProviderType.Local, PendingSeedName, payload));
        return JsonSerializer.Deserialize<SeedDatabaseRequest>(json);
    }

        /// <summary>
    /// ClearAsync method.
    /// </summary>
public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var root = await ReadOrCreateAsync(env, cancellationToken);
        var bootstrap = root["AeroCms"]?["Bootstrap"] as JsonObject;
        bootstrap?.Remove(PendingSeedKey);

        await appSettingsWriter.WriteAsync(env, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
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
        if (!File.Exists(path))
        {
            return new JsonObject();
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
    }
}
