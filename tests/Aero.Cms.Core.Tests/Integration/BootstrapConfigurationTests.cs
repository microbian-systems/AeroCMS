using Aero.AppServer;
using Aero.AppServer.Startup;
using Aero.Cms.Abstractions.Authentication;
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
                ["AeroCms:Infrastructure:DatabaseMode"] = "Embedded",
                ["AeroCms:Infrastructure:CacheMode"] = "Local",
                ["AeroCms:Infrastructure:SecretProvider"] = "Local Certificate"
            })
            .Build();

        var state = new AppSettingsBootstrapStateProvider(config).GetState();

        state.State.Should().Be(BootstrapStates.Setup);
        state.HasBootstrapConfig.Should().BeFalse();
        state.DatabaseMode.Should().Be("Embedded");
        state.CacheMode.Should().Be("Local");
        state.SecretProvider.Should().Be("Local Certificate");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Appsettings_bootstrap_provider_reads_independent_authentication_selections()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AeroCms:Bootstrap:RequestedManagerAuthenticationProvider"] = AuthenticationProviderSelections.Manager.Local,
                ["AeroCms:Bootstrap:RequestedMemberAuthenticationProvider"] = AuthenticationProviderSelections.Member.WorkOs
            })
            .Build();

        var state = new AppSettingsBootstrapStateProvider(config).GetState();

        state.RequestedManagerAuthenticationProvider.Should().Be(AuthenticationProviderSelections.Manager.Local);
        state.RequestedMemberAuthenticationProvider.Should().Be(AuthenticationProviderSelections.Member.WorkOs);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Appsettings_bootstrap_provider_defaults_authentication_selections_without_legacy_mode()
    {
        var state = new AppSettingsBootstrapStateProvider(new ConfigurationBuilder().Build()).GetState();

        state.RequestedManagerAuthenticationProvider.Should().Be(AuthenticationProviderSelections.Manager.Local);
        state.RequestedMemberAuthenticationProvider.Should().Be(AuthenticationProviderSelections.Member.Disabled);

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
    public async Task Infrastructure_resolver_rejects_setup_state_even_when_topology_is_preconfigured()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AeroCms:Bootstrap:State"] = BootstrapStates.Setup,
                ["AeroCms:Bootstrap:HasBootstrapConfig"] = "false",
                ["AeroCms:Infrastructure:DatabaseMode"] = "Embedded",
                ["AeroCms:Infrastructure:CacheMode"] = "Local",
                ["AeroCms:Infrastructure:SecretProvider"] = "Local Certificate"
            })
            .Build();

        var action = () => new InfrastructureConnectionStringResolver(config).Resolve();

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*before setup reaches Configured or Running state*");

        await Task.CompletedTask;
}

    [Test]
    public void Infrastructure_resolver_rejects_removed_memory_cache_mode()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AeroCms:Bootstrap:State"] = BootstrapStates.Configured,
                ["AeroCms:Bootstrap:HasBootstrapConfig"] = "true",
                ["AeroCms:Infrastructure:DatabaseMode"] = "Embedded",
                ["AeroCms:Infrastructure:CacheMode"] = "Memory",
                ["AeroCms:Infrastructure:SecretProvider"] = "Local Certificate"
            })
            .Build();

        var action = () => new InfrastructureConnectionStringResolver(config).Resolve();

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Expected 'Local' or 'Server'*");
    }

    [Test]
    public void Infrastructure_resolver_defaults_and_overrides_database_scope_independently()
    {
        using var fixture = CreateApplicationServerBuilder(AeroAppServerConstants.LocalCacheMode);
        fixture.Builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AeroCms:Infrastructure:DatabaseNamespace"] = "wildlife-prod"
        });

        var resolved = new InfrastructureConnectionStringResolver(fixture.Builder.Configuration).Resolve();

        resolved.DatabaseNamespace.Should().Be("wildlife-prod");
        resolved.DatabaseName.Should().Be("aero");
    }

    [Test]
    public void Infrastructure_resolver_rejects_an_explicit_invalid_database_scope()
    {
        using var fixture = CreateApplicationServerBuilder(AeroAppServerConstants.LocalCacheMode);
        fixture.Builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AeroCms:Infrastructure:DatabaseName"] = "invalid database"
        });

        var action = () => new InfrastructureConnectionStringResolver(fixture.Builder.Configuration).Resolve();

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*SurrealDB database name is invalid*");
    }

    [Test]
    public async Task Local_cache_mode_registers_the_in_process_garnet_host()
    {
        using var fixture = CreateApplicationServerBuilder(AeroAppServerConstants.LocalCacheMode);
        var builder = fixture.Builder;

        await builder.AddAeroApplicationServer();

        builder.Services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType != null
            && descriptor.ImplementationType.Name == "AeroCacheService");
    }

    [Test]
    public async Task Server_cache_mode_does_not_register_the_in_process_garnet_host()
    {
        using var fixture = CreateApplicationServerBuilder(AeroAppServerConstants.ServerCacheMode);
        var builder = fixture.Builder;

        await builder.AddAeroApplicationServer();

        builder.Services.Should().NotContain(descriptor =>
            descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType != null
            && descriptor.ImplementationType.Name == "AeroCacheService");
    }

    private static ApplicationServerBuilderFixture CreateApplicationServerBuilder(string cacheMode)
    {
        var secretRoot = Path.Combine(Path.GetTempPath(), "AeroCmsTests", Path.GetRandomFileName());
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AeroCms:Bootstrap:State"] = BootstrapStates.Configured,
            ["AeroCms:Bootstrap:HasBootstrapConfig"] = "true",
            ["AeroCms:Infrastructure:DatabaseMode"] = "Embedded",
            ["AeroCms:Infrastructure:CacheMode"] = cacheMode,
            ["AeroCms:Infrastructure:SecretProvider"] = "Local Certificate",
            ["AeroCms:DataProtection:Certificate:Path"] = Path.Combine(secretRoot, "aero.pfx"),
            ["AeroCms:DataProtection:KeyStoragePath"] = Path.Combine(secretRoot, "keys")
        });

        if (cacheMode.Equals(AeroAppServerConstants.ServerCacheMode, StringComparison.OrdinalIgnoreCase))
        {
            var secretManager = DataProtectionCertificateBootstrapper.CreateSecretManager(builder.Configuration);
            var stored = secretManager.Store("127.0.0.1:6379", "cache");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AeroCms:Infrastructure:CacheConnectionStringReference"] = stored.Metadata ?? stored.Value
            });
        }

        return new ApplicationServerBuilderFixture(builder, secretRoot);
    }

    private sealed record ApplicationServerBuilderFixture(
        HostApplicationBuilder Builder,
        string SecretRoot) : IDisposable
    {
        public void Dispose()
        {
            if (Directory.Exists(SecretRoot))
            {
                Directory.Delete(SecretRoot, recursive: true);
            }
        }
    }
}
