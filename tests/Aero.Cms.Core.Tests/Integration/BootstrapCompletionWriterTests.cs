using System.Text.Json.Nodes;
using Aero.Cms.Modules.Setup;
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
        var environment = "UnitTest";
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
}
