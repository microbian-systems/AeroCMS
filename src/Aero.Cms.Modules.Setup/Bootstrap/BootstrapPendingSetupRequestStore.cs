using System.Text.Json;
using System.Text.Json.Nodes;
using Aero.Cms.Modules.Setup.Configuration;
using Aero.Secrets;
using Aero.Secrets.Models;

namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Stores the setup request that must survive the handoff from the setup host to runtime initialization.
/// </summary>
public interface IBootstrapPendingSetupRequestStore
{
    /// <summary>
    /// Protects and persists a pending setup request.
    /// </summary>
    /// <param name="request">The request, including any credentials needed during runtime seeding.</param>
    /// <param name="cancellationToken">Cancels file reads or the atomic settings write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
Task SaveAsync(SeedDatabaseRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads and decrypts the pending setup request.
    /// </summary>
    /// <param name="cancellationToken">Cancels reading the settings file.</param>
    /// <returns>The pending request, or <see langword="null"/> when no protected payload is configured.</returns>
Task<SeedDatabaseRequest?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the persisted pending payload after runtime setup succeeds.
    /// </summary>
    /// <param name="cancellationToken">Cancels file reads or the atomic settings write.</param>
Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists the pending setup request as a locally protected secret reference in application settings.
/// </summary>
/// <remarks>
/// The request is serialized before it is handed to <see cref="ISecretManager"/>; plaintext
/// credentials are not written directly to the bootstrap section. Secret-provider and JSON
/// failures propagate to the caller.
/// </remarks>
public sealed class BootstrapPendingSetupRequestStore(
    IEnvironmentAppSettingsWriter appSettingsWriter,
    ISecretManager secretManager) : IBootstrapPendingSetupRequestStore
{
    private const string PendingSeedKey = "PendingSeedPayload";
    private const string PendingSeedName = "AeroCms:Bootstrap:PendingSeed";

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var root = await ReadOrCreateAsync(env, cancellationToken);
        var bootstrap = root["AeroCms"]?["Bootstrap"] as JsonObject;
        bootstrap?.Remove(PendingSeedKey);

        await appSettingsWriter.WriteAsync(env, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
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
    /// Reads the environment settings object, returning an empty object when the file is absent or contains JSON <see langword="null"/>.
    /// </summary>
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
