using Aero.AppServer;
using Aero.AppServer.Startup;
using Aero.Cms.Modules.Setup.Bootstrap;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
                ["AeroCms:Bootstrap:CacheMode"] = "Local",
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
                ["AeroCms:Bootstrap:CacheMode"] = "Local",
                ["AeroCms:Bootstrap:SecretProvider"] = "Local Certificate"
            })
            .Build();

        var resolved = new InfrastructureConnectionStringResolver(config).Resolve();

        resolved.DatabaseConnectionString.Should().Be("surrealkv://App_Data/aerodb-surrealkv");
        resolved.CacheConnectionString.Should().Be(AeroAppServerConstants.CacheUrl);
        resolved.DatabaseMode.Should().Be("Embedded");
        resolved.CacheMode.Should().Be(AeroAppServerConstants.LocalCacheMode);

        await Task.CompletedTask;
}

    [Test]
    public void Infrastructure_resolver_rejects_removed_memory_cache_mode()
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

        var action = () => new InfrastructureConnectionStringResolver(config).Resolve();

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Expected 'Local' or 'Server'*");
    }

    [Test]
    public async Task Local_cache_mode_registers_the_in_process_garnet_host()
    {
        var builder = CreateApplicationServerBuilder(AeroAppServerConstants.LocalCacheMode);

        await builder.AddAeroApplicationServer();

        builder.Services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType != null
            && descriptor.ImplementationType.Name == "AeroCacheService");
    }

    [Test]
    public async Task Server_cache_mode_does_not_register_the_in_process_garnet_host()
    {
        var builder = CreateApplicationServerBuilder(AeroAppServerConstants.ServerCacheMode);

        await builder.AddAeroApplicationServer();

        builder.Services.Should().NotContain(descriptor =>
            descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType != null
            && descriptor.ImplementationType.Name == "AeroCacheService");
    }

    private static HostApplicationBuilder CreateApplicationServerBuilder(string cacheMode)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AeroCms:Bootstrap:State"] = BootstrapStates.Setup,
            ["AeroCms:Bootstrap:HasBootstrapConfig"] = "false",
            ["AeroCms:Bootstrap:DatabaseMode"] = "Embedded",
            ["AeroCms:Bootstrap:CacheMode"] = cacheMode,
            ["AeroCms:Bootstrap:SecretProvider"] = "Local Certificate"
        });
        return builder;
    }
}
