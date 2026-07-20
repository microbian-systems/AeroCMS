using System.Text.Json;
using System.Text.Json.Nodes;
using Aero.Cms.Modules.Setup.Configuration;

namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Persists lifecycle transitions for the bootstrap process.
/// </summary>
public interface IBootstrapCompletionWriter
{
    /// <summary>
    /// Marks bootstrap and seeding as complete and changes the persisted state to running.
    /// </summary>
    /// <param name="cancellationToken">Cancels file reads or the atomic settings write.</param>
Task MarkCompleteAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Records that configuration is available but runtime seeding has not completed.
    /// </summary>
    /// <param name="cancellationToken">Cancels file reads or the atomic settings write.</param>
Task MarkConfiguredAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Records a failed bootstrap attempt while retaining the fact that bootstrap configuration exists.
    /// </summary>
    /// <param name="cancellationToken">Cancels file reads or the atomic settings write.</param>
Task MarkFailedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Updates the bootstrap section of the environment-specific web application settings file.
/// </summary>
/// <remarks>
/// Existing settings outside <c>AeroCms:Bootstrap</c> are preserved. Missing files and
/// missing object sections are created. Malformed JSON and file-system failures propagate
/// to the caller.
/// </remarks>
public sealed class BootstrapCompletionWriter(IEnvironmentAppSettingsWriter appSettingsWriter) : IBootstrapCompletionWriter
{
    /// <inheritdoc />
public async Task MarkCompleteAsync(CancellationToken cancellationToken = default)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var path = AppSettingsPathResolver.GetAppSettingsFilePath(env);
        JsonObject root;

        if (File.Exists(path))
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken);
            root = JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        var aeroCms = root["AeroCms"] as JsonObject ?? new JsonObject();
        root["AeroCms"] = aeroCms;

        var bootstrap = aeroCms["Bootstrap"] as JsonObject ?? new JsonObject();
        aeroCms["Bootstrap"] = bootstrap;

        bootstrap["State"] = BootstrapStates.Running;
        bootstrap["HasBootstrapConfig"] = true;
        bootstrap["SetupComplete"] = true;
        bootstrap["SeedComplete"] = true;

        await appSettingsWriter.WriteAsync(env, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
    }

    /// <inheritdoc />
public async Task MarkConfiguredAsync(CancellationToken cancellationToken = default)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var path = AppSettingsPathResolver.GetAppSettingsFilePath(env);
        JsonObject root;

        if (File.Exists(path))
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken);
            root = JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        var aeroCms = root["AeroCms"] as JsonObject ?? new JsonObject();
        root["AeroCms"] = aeroCms;

        var bootstrap = aeroCms["Bootstrap"] as JsonObject ?? new JsonObject();
        aeroCms["Bootstrap"] = bootstrap;

        // Mark as Configured - runtime bootstrap still pending.
        bootstrap["State"] = BootstrapStates.Configured;
        bootstrap["HasBootstrapConfig"] = true;
        bootstrap["SetupComplete"] = false;
        bootstrap["SeedComplete"] = false;

        await appSettingsWriter.WriteAsync(env, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
    }

    /// <inheritdoc />
public async Task MarkFailedAsync(CancellationToken cancellationToken = default)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var path = AppSettingsPathResolver.GetAppSettingsFilePath(env);
        JsonObject root;

        if (File.Exists(path))
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken);
            root = JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        var aeroCms = root["AeroCms"] as JsonObject ?? new JsonObject();
        root["AeroCms"] = aeroCms;

        var bootstrap = aeroCms["Bootstrap"] as JsonObject ?? new JsonObject();
        aeroCms["Bootstrap"] = bootstrap;

        bootstrap["State"] = BootstrapStates.Failed;
        bootstrap["HasBootstrapConfig"] = true;
        bootstrap["SetupComplete"] = false;
        bootstrap["SeedComplete"] = false;

        await appSettingsWriter.WriteAsync(env, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
    }
}
