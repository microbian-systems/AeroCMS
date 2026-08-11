using System.Text.Json.Nodes;
using Aero.AppServer;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.Modules.Setup.Configuration;
using Aero.Secrets;
using Aero.Secrets.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Integration;

public class BootstrapEnvironmentPersistenceTests
{
    [Test]
    public async Task Setup_persistence_uses_the_host_selected_environment_for_every_artifact()
    {
        var writer = Substitute.For<IEnvironmentAppSettingsWriter>();
        var missingFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "appsettings.Staging.json");
        writer.GetFilePath("Staging").Returns(missingFile);

        var secretManager = Substitute.For<ISecretManager>();
        secretManager.Store(Arg.Any<string>(), Arg.Any<string>(), SecretProviderType.Local)
            .Returns(call => new StoredSecretReference(
                SecretProviderType.Local,
                call.ArgAt<string>(1),
                "protected"));

        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Staging");
        var infisical = new InfisicalBootstrapSettingsProvider(new ConfigurationBuilder().Build());
        var database = new DatabaseBootstrapService(
            writer,
            secretManager,
            infisical,
            environment);
        var cache = new CacheBootstrapService(writer, secretManager, infisical, environment);
        var pending = new BootstrapPendingSetupRequestStore(writer, secretManager, environment);

        await database.PersistAsync(new DatabaseBootstrapModel(
            "Embedded",
            null,
            "Local Certificate",
            AuthenticationProviderSelections.Manager.Local,
            AuthenticationProviderSelections.Member.Disabled)
        {
            DatabaseNamespace = "wildlife-prod",
            DatabaseName = "cms_data"
        });
        await cache.PersistAsync(new CacheBootstrapModel(
            AeroAppServerConstants.LocalCacheMode,
            null,
            "Local Certificate"));
        await pending.SaveAsync(CreateRequest());
        await pending.LoadAsync();
        await pending.ClearAsync();

        writer.Received().WriteAsync(
            "Staging",
            Arg.Is<string>(content => ContainsDatabaseScope(content)),
            Arg.Any<CancellationToken>());
        await writer.Received(4).WriteAsync(
            "Staging",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        writer.DidNotReceive().GetFilePath("Development");
        await writer.DidNotReceive().WriteAsync(
            "Development",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    private static bool ContainsDatabaseScope(string json)
    {
        var root = JsonNode.Parse(json);
        return root?["AeroCms"]?["Bootstrap"]?["State"]?.GetValue<string>() == BootstrapStates.Setup
               && root?["AeroCms"]?["Infrastructure"]?["DatabaseNamespace"]?.GetValue<string>() == "wildlife-prod"
               && root?["AeroCms"]?["Infrastructure"]?["DatabaseName"]?.GetValue<string>() == "cms_data";
    }

    private static SeedDatabaseRequest CreateRequest()
        => new(
            "Embedded",
            AeroAppServerConstants.LocalCacheMode,
            "Local Certificate",
            AuthenticationProviderSelections.Manager.Local,
            AuthenticationProviderSelections.Member.Disabled,
            null,
            null,
            null,
            null,
            "admin",
            "admin@example.com",
            "correct horse battery",
            "Aero CMS",
            "Welcome",
            "Blog",
            "localhost",
            "en-US",
            ["en-US"]);
}
