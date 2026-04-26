using TUnit.Core;
using Aero.AppServer;
using Aero.AppServer.Startup;
using Aero.Cms.Modules.Setup.Bootstrap;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Aero.Cms.Core.Tests.Integration;

public class BootstrapConfigurationTests
{
    [Test]
    public async Task Appsettings_bootstrap_provider_respects_HasBootstrapConfig_flag()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AeroCms:Bootstrap:State"] = BootstrapStates.Setup,
                ["AeroCms:Bootstrap:HasBootstrapConfig"] = "false",
                ["AeroCms:Bootstrap:DatabaseMode"] = "Embedded",
                ["AeroCms:Bootstrap:CacheMode"] = "Memory",
                ["AeroCms:Bootstrap:SecretProvider"] = "Local Certificate"
            })
            .Build();

        var state = new AppSettingsBootstrapStateProvider(config).GetState();

        state.State.Should().Be(BootstrapStates.Setup);
        state.HasBootstrapConfig.Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Data_protection_bootstrapper_reads_AeroCms_data_protection_section()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AeroCms:DataProtection:KeyStoragePath"] = "keys-test",
                ["AeroCms:DataProtection:ApplicationName"] = "AeroCMS-Test",
                ["AeroCms:DataProtection:Certificate:Path"] = "certs/test-cert.pfx",
                ["AeroCms:DataProtection:Certificate:Password"] = "secret"
            })
            .Build();

        var settings = DataProtectionCertificateBootstrapper.ResolveSettings(config);

        settings.KeyRingPath.Should().Be("keys-test");
        settings.ApplicationName.Should().Be("AeroCMS-Test");
        settings.CertificatePath.Should().Be("certs/test-cert.pfx");
        settings.CertificatePassword.Should().Be("secret");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Infrastructure_resolver_uses_embedded_defaults_when_bootstrap_is_not_configured()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AeroCms:Bootstrap:State"] = BootstrapStates.Setup,
                ["AeroCms:Bootstrap:HasBootstrapConfig"] = "false",
                ["AeroCms:Bootstrap:DatabaseMode"] = "Embedded",
                ["AeroCms:Bootstrap:CacheMode"] = "Memory",
                ["AeroCms:Bootstrap:SecretProvider"] = "Local Certificate"
            })
            .Build();

        var resolved = new InfrastructureConnectionStringResolver(config).Resolve();

        resolved.DatabaseConnectionString.Should().Be(AeroAppServerConstants.EmbedConnString);
        resolved.CacheConnectionString.Should().BeNull();
        resolved.DatabaseMode.Should().Be("Embedded");
        resolved.CacheMode.Should().Be("Memory");

        await Task.CompletedTask;
}
}
