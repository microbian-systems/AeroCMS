using TUnit.Core;
using System.Text.Json.Nodes;
using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.Modules.Setup.Configuration;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Integration;

public class BootstrapCompletionWriterTests
{
    [Test]
    public async Task Mark_complete_merges_flags_into_existing_appsettings_json()
    {
        var environment = $"UnitTest_{Guid.NewGuid():N}";
        var webProjectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Aero.Cms.Web"));
        var filePath = Path.Combine(webProjectPath, $"appsettings.{environment}.json");
        var originalEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var writer = Substitute.For<IEnvironmentAppSettingsWriter>();
        string? writtenJson = null;

        try
        {
            Directory.CreateDirectory(webProjectPath);
            await File.WriteAllTextAsync(filePath, """
            {
              "Logging": { "Level": "Debug" },
              "AeroCms": { "Bootstrap": { "Existing": true } },
              "FeatureFlag": true
            }
            """);

            writer.WriteAsync(environment, Arg.Do<string>(json => writtenJson = json), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);

            var completionWriter = new BootstrapCompletionWriter(writer);

            await completionWriter.MarkCompleteAsync();

            writtenJson.Should().NotBeNull();
            var root = JsonNode.Parse(writtenJson!)!.AsObject();
            root["FeatureFlag"]!.GetValue<bool>().Should().BeTrue();
            root["AeroCms"]!["Bootstrap"]!["Existing"]!.GetValue<bool>().Should().BeTrue();
            root["AeroCms"]!["Bootstrap"]!["SetupComplete"]!.GetValue<bool>().Should().BeTrue();
            root["AeroCms"]!["Bootstrap"]!["SeedComplete"]!.GetValue<bool>().Should().BeTrue();

            await writer.Received(1).WriteAsync(environment, Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnvironment);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Test]
    public async Task Mark_configured_keeps_bootstrap_pending_until_runtime_initialization_finishes()
    {
        var environment = $"UnitTest_{Guid.NewGuid():N}";
        var webProjectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Aero.Cms.Web"));
        var filePath = Path.Combine(webProjectPath, $"appsettings.{environment}.json");
        var originalEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var writer = Substitute.For<IEnvironmentAppSettingsWriter>();
        string? writtenJson = null;

        try
        {
            Directory.CreateDirectory(webProjectPath);
            await File.WriteAllTextAsync(filePath, """
            {
              "AeroCms": { "Bootstrap": { "Existing": true, "SetupComplete": true, "SeedComplete": true } }
            }
            """);

            writer.WriteAsync(environment, Arg.Do<string>(json => writtenJson = json), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);

            var completionWriter = new BootstrapCompletionWriter(writer);

            await completionWriter.MarkConfiguredAsync();

            writtenJson.Should().NotBeNull();
            var root = JsonNode.Parse(writtenJson!)!.AsObject();
            root["AeroCms"]!["Bootstrap"]!["State"]!.GetValue<string>().Should().Be(BootstrapStates.Configured);
            root["AeroCms"]!["Bootstrap"]!["HasBootstrapConfig"]!.GetValue<bool>().Should().BeTrue();
            root["AeroCms"]!["Bootstrap"]!["SetupComplete"]!.GetValue<bool>().Should().BeFalse();
            root["AeroCms"]!["Bootstrap"]!["SeedComplete"]!.GetValue<bool>().Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnvironment);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Test]
    public async Task Mark_failed_sets_failed_state_and_disables_setup_completion_flags()
    {
        var environment = $"UnitTest_{Guid.NewGuid():N}";
        var webProjectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Aero.Cms.Web"));
        var filePath = Path.Combine(webProjectPath, $"appsettings.{environment}.json");
        var originalEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var writer = Substitute.For<IEnvironmentAppSettingsWriter>();
        string? writtenJson = null;

        try
        {
            Directory.CreateDirectory(webProjectPath);
            await File.WriteAllTextAsync(filePath, """
            {
              "AeroCms": { "Bootstrap": { "Existing": true } }
            }
            """);

            writer.WriteAsync(environment, Arg.Do<string>(json => writtenJson = json), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);

            var completionWriter = new BootstrapCompletionWriter(writer);

            await completionWriter.MarkFailedAsync();

            writtenJson.Should().NotBeNull();
            var root = JsonNode.Parse(writtenJson!)!.AsObject();
            root["AeroCms"]!["Bootstrap"]!["State"]!.GetValue<string>().Should().Be(BootstrapStates.Failed);
            root["AeroCms"]!["Bootstrap"]!["HasBootstrapConfig"]!.GetValue<bool>().Should().BeTrue();
            root["AeroCms"]!["Bootstrap"]!["SetupComplete"]!.GetValue<bool>().Should().BeFalse();
            root["AeroCms"]!["Bootstrap"]!["SeedComplete"]!.GetValue<bool>().Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnvironment);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
}
        }
    }
}
