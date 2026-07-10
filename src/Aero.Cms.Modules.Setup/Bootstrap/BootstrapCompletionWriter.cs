using System.Text.Json;
using System.Text.Json.Nodes;
using Aero.Cms.Modules.Setup.Configuration;

namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Defines an interface for IBootstrapCompletionWriter.
/// </summary>
public interface IBootstrapCompletionWriter
{
        /// <summary>
    /// MarkCompleteAsync method.
    /// </summary>
Task MarkCompleteAsync(CancellationToken cancellationToken = default);
        /// <summary>
    /// MarkConfiguredAsync method.
    /// </summary>
Task MarkConfiguredAsync(CancellationToken cancellationToken = default);
        /// <summary>
    /// MarkFailedAsync method.
    /// </summary>
Task MarkFailedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a class for BootstrapCompletionWriter.
/// </summary>
public sealed class BootstrapCompletionWriter(IEnvironmentAppSettingsWriter appSettingsWriter) : IBootstrapCompletionWriter
{
        /// <summary>
    /// MarkCompleteAsync method.
    /// </summary>
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

        /// <summary>
    /// MarkConfiguredAsync method.
    /// </summary>
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

        /// <summary>
    /// MarkFailedAsync method.
    /// </summary>
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
