using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.Modules.Setup.Configuration;

namespace Aero.Cms.Modules.Setup;

public interface IBootstrapCompletionWriter
{
    Task MarkCompleteAsync(CancellationToken cancellationToken = default);
    Task MarkConfiguredAsync(CancellationToken cancellationToken = default);
}

public sealed class BootstrapCompletionWriter(IEnvironmentAppSettingsWriter appSettingsWriter) : IBootstrapCompletionWriter
{
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
}
