using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Identity;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ManagerFederationFoundationTests
{
    private const string TenantId = "11111111-2222-3333-4444-555555555555";

    [Test]
    public async Task Authority_is_installation_wide_and_remains_inactive_until_verified_link()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(options => new IdentityModule().Configure(options));
        await harness.InitializeAsync();
        var service = new ManagerIdentityAuthorityService(harness.Session,
            new ConfigureManagerIdentityAuthorityRequestValidator(), TimeProvider.System,
            PendingMode(ManagerIdentityProviders.EntraWorkforce));

        var result = Ok(await service.ConfigureAsync(new(
            ManagerIdentityProviders.EntraWorkforce,
            TenantId,
            $"https://login.microsoftonline.com/{TenantId}/v2.0",
            "https://cms.example.com",
            17,
            "development")));
        var stored = (await harness.Session.LoadAsync<ManagerIdentityAuthorityBinding>(result.BindingId))!;

        await Assert.That(stored.IsVerified).IsFalse();
        await Assert.That(stored.IsActive).IsFalse();
        await Assert.That(stored.CredentialPath).IsEqualTo(
            ManagerProviderSecretReference.CanonicalCredentialPath(ManagerIdentityProviders.EntraWorkforce));
    }

    [Test]
    public async Task Authority_validator_rejects_common_tenant_and_noncanonical_workforce_authority()
    {
        var validator = new ConfigureManagerIdentityAuthorityRequestValidator();
        var result = await validator.ValidateAsync(new ConfigureManagerIdentityAuthorityRequest(
            ManagerIdentityProviders.EntraWorkforce,
            TenantId,
            "https://login.microsoftonline.com/common/v2.0",
            "https://cms.example.com",
            1,
            "production"));

        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task Verified_authority_rejects_credential_reference_changes_without_mutating_binding()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(options => new IdentityModule().Configure(options));
        await harness.InitializeAsync();
        var service = new ManagerIdentityAuthorityService(harness.Session,
            new ConfigureManagerIdentityAuthorityRequestValidator(), TimeProvider.System,
            PendingMode(ManagerIdentityProviders.WorkOs));
        var request = new ConfigureManagerIdentityAuthorityRequest(
            ManagerIdentityProviders.WorkOs, "org_123", "https://api.workos.com",
            "https://cms.example.com", 17, "production");
        var created = Ok(await service.ConfigureAsync(request));
        var binding = (await harness.Session.LoadAsync<ManagerIdentityAuthorityBinding>(created.BindingId))!;
        binding.IsVerified = true;
        binding.IsActive = true;
        harness.Session.Store(binding);
        await harness.Session.SaveChangesAsync();

        var changed = await service.ConfigureAsync(request with { VaultId = 18, VaultEnvironment = "staging" });
        var preserved = (await harness.Session.LoadAsync<ManagerIdentityAuthorityBinding>(created.BindingId))!;

        await Assert.That(changed).IsTypeOf<Result<ManagerIdentityAuthorityResult, AeroError>.Failure>();
        await Assert.That(preserved.VaultId).IsEqualTo(17);
        await Assert.That(preserved.VaultEnvironment).IsEqualTo("production");
        await Assert.That(preserved.IsActive).IsTrue();
        await Assert.That(preserved.IsVerified).IsTrue();
    }

    [Test]
    public async Task Singleton_discriminator_prevents_a_second_installation_authority()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(options => new IdentityModule().Configure(options));
        await harness.InitializeAsync();
        harness.Session.Store(new ManagerIdentityAuthorityBinding
        {
            Id = Snowflake.NewId(),
            SingletonKey = ManagerIdentityAuthorityBinding.InstallationSingletonKey,
            Provider = ManagerIdentityProviders.WorkOs,
            Issuer = "https://api.workos.com",
            OrganizationId = "org_one",
            Authority = "https://api.workos.com",
            BindingKey = "first",
            VaultId = 1,
            VaultEnvironment = "production",
            CredentialPath = ManagerProviderSecretReference.CanonicalCredentialPath(ManagerIdentityProviders.WorkOs)
        });
        harness.Session.Store(new ManagerIdentityAuthorityBinding
        {
            Id = Snowflake.NewId(),
            SingletonKey = ManagerIdentityAuthorityBinding.InstallationSingletonKey,
            Provider = ManagerIdentityProviders.WorkOs,
            Issuer = "https://api.workos.com",
            OrganizationId = "org_two",
            Authority = "https://api.workos.com",
            BindingKey = "second",
            VaultId = 2,
            VaultEnvironment = "production",
            CredentialPath = ManagerProviderSecretReference.CanonicalCredentialPath(ManagerIdentityProviders.WorkOs)
        });

        await Assert.That(async () => await harness.Session.SaveChangesAsync()).ThrowsException();
    }

    [Test]
    public async Task Manager_credentials_are_a_separate_zeroing_boundary()
    {
        using var credentials = new ManagerProviderCredentialBundle([1, 2], [3, 4], [5, 6]);
        var clientId = credentials.LeaseClientId();
        var clientSecret = credentials.LeaseClientSecret();
        var apiKey = credentials.LeaseApiKey();
        credentials.Dispose();

        await Assert.That(clientId.Bytes.ToArray()).IsEquivalentTo(new byte[] { 0, 0 });
        await Assert.That(clientSecret.Bytes.ToArray()).IsEquivalentTo(new byte[] { 0, 0 });
        await Assert.That(apiKey.Bytes.ToArray()).IsEquivalentTo(new byte[] { 0, 0 });
    }

    [Test]
    public async Task Development_secret_source_requires_both_environment_and_explicit_gate()
    {
        var services = new ServiceCollection();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        var disabled = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton<IConfiguration>(disabled);
        new IdentityModule().ConfigureServices(services, disabled, environment);
        using var disabledProvider = services.BuildServiceProvider();
        await Assert.That(disabledProvider.GetRequiredService<IManagerProviderSecretSource>())
            .IsTypeOf<UnavailableManagerProviderSecretSource>();

        var enabledValues = new Dictionary<string, string?>
        {
            [DevelopmentManagerProviderSecretSource.EnabledConfigurationKey] = "true",
            [$"AeroCms:Authentication:ManagerFederation:DevelopmentSecrets:{ManagerIdentityProviders.WorkOs}:ClientId"] = "client",
            [$"AeroCms:Authentication:ManagerFederation:DevelopmentSecrets:{ManagerIdentityProviders.WorkOs}:ApiKey"] = "secret"
        };
        var enabledServices = new ServiceCollection();
        var enabled = new ConfigurationBuilder().AddInMemoryCollection(enabledValues).Build();
        enabledServices.AddSingleton<IConfiguration>(enabled);
        new IdentityModule().ConfigureServices(enabledServices, enabled, environment);
        using var enabledProvider = enabledServices.BuildServiceProvider();
        await Assert.That(enabledProvider.GetRequiredService<IManagerProviderSecretSource>())
            .IsTypeOf<DevelopmentManagerProviderSecretSource>();
    }

    private static T Ok<T>(Result<T, AeroError> result) =>
        result is Result<T, AeroError>.Ok(var value)
            ? value
            : throw new InvalidOperationException("Expected success.");

    private static IManagerAuthenticationModeResolver PendingMode(string requestedProvider)
    {
        var resolver = Substitute.For<IManagerAuthenticationModeResolver>();
        resolver.ResolveAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(
            Prelude.Ok<ManagerAuthenticationModeResolution, AeroError>(new(
                requestedProvider,
                AuthenticationProviderSelections.Manager.Local,
                ManagerAuthenticationModeStatuses.Pending,
                null))));
        return resolver;
    }
}
