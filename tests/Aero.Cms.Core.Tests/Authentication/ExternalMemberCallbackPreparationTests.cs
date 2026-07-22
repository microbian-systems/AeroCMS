using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Identity;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Models.Entities;
using AeroDB.Sable;
using Microsoft.Extensions.DependencyInjection;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ExternalMemberCallbackPreparationTests
{
    private const long TenantId = 4111;
    private const long SiteId = 4211;
    private const long BindingId = 4311;

    [Test]
    public async Task Prepare_succeeds_without_consuming_state()
    {
        await using var setup = await CreateSetupAsync();
        var state = Ok(await setup.Service.BeginAsync(Request()));

        var prepared = Ok(await setup.Service.PrepareCallbackAsync(state.Handle, TenantId, SiteId, ExternalMemberProviders.WorkOs));
        var stored = await setup.Harness.Session.LoadAsync<ExternalAuthenticationState>(StateId(state.Handle));

        await Assert.That(prepared.OrganizationBindingId).IsEqualTo(BindingId);
        await Assert.That(prepared.ProtectedProviderCorrelation).IsEqualTo("sealed-correlation");
        await Assert.That(prepared.ReturnPath).IsEqualTo("/shop/checkout");
        await Assert.That(stored!.ConsumedAt).IsNull();
    }

    [Test]
    [Arguments("forged")]
    [Arguments("tenant")]
    [Arguments("site")]
    [Arguments("provider")]
    [Arguments("expired")]
    [Arguments("binding")]
    public async Task Prepare_rejects_handle_and_scope_mixups(string scenario)
    {
        await using var setup = await CreateSetupAsync();
        var state = Ok(await setup.Service.BeginAsync(Request()));
        var handle = scenario == "forged" ? FlipLastHandleCharacter(state.Handle) : state.Handle;
        if (scenario == "expired")
        {
            var stored = (await setup.Harness.Session.LoadAsync<ExternalAuthenticationState>(StateId(state.Handle)))!;
            stored.ExpiresAt = setup.Time.GetUtcNow().AddSeconds(-1);
            setup.Harness.Session.Store(stored);
        }
        if (scenario == "binding")
        {
            var binding = (await setup.Harness.Session.LoadAsync<ExternalOrganizationBinding>(BindingId))!;
            binding.CredentialPath = "/wrong";
            setup.Harness.Session.Store(binding);
        }
        if (scenario is "expired" or "binding") await setup.Harness.Session.SaveChangesAsync();

        var result = await setup.Service.PrepareCallbackAsync(handle,
            scenario == "tenant" ? TenantId + 1 : TenantId,
            scenario == "site" ? SiteId + 1 : SiteId,
            scenario == "provider" ? ExternalMemberProviders.EntraExternalId : ExternalMemberProviders.WorkOs);

        await Assert.That(result).IsTypeOf<Result<ExternalMemberCallbackPreparation, AeroError>.Failure>();
    }

    [Test]
    public async Task Prepare_rejects_consumed_state()
    {
        await using var setup = await CreateSetupAsync();
        var state = Ok(await setup.Service.BeginAsync(Request()));
        var stored = (await setup.Harness.Session.LoadAsync<ExternalAuthenticationState>(StateId(state.Handle)))!;
        stored.ConsumedAt = setup.Time.GetUtcNow();
        setup.Harness.Session.Store(stored);
        await setup.Harness.Session.SaveChangesAsync();

        var result = await setup.Service.PrepareCallbackAsync(
            state.Handle,
            TenantId,
            SiteId,
            ExternalMemberProviders.WorkOs);

        await Assert.That(result).IsTypeOf<Result<ExternalMemberCallbackPreparation, AeroError>.Failure>();
    }

    [Test]
    public async Task Correlation_is_bounded_and_provider_constants_and_secret_seam_are_fail_closed()
    {
        await using var setup = await CreateSetupAsync();
        var invalid = await setup.Service.BeginAsync(Request() with { ProtectedProviderCorrelation = new string('a', 2049) });
        await Assert.That(invalid).IsTypeOf<Result<ExternalMemberAuthenticationHandle, AeroError>.Failure>();
        await Assert.That(ExternalMemberProviders.WorkOs).IsEqualTo("workos");
        await Assert.That(ExternalMemberProviders.EntraExternalId).IsEqualTo("entra_external_id");

        var source = new UnavailableExternalProviderSecretSource();
        var unavailable = await source.ReadAsync(new(1, "production", TenantId, ExternalMemberProviders.WorkOs,
            ExternalProviderSecretReference.CanonicalCredentialPath(TenantId, ExternalMemberProviders.WorkOs)));
        await Assert.That(unavailable).IsTypeOf<Result<ExternalProviderCredentialBundle, AeroError>.Failure>();
    }

    [Test]
    public async Task Credential_bundle_zeroes_owned_bytes_through_leases()
    {
        using var bundle = new ExternalProviderCredentialBundle([1, 2], [3, 4], [5, 6]);
        var clientId = bundle.LeaseClientId();
        var clientSecret = bundle.LeaseClientSecret();
        var apiKey = bundle.LeaseApiKey();
        bundle.Dispose();

        await Assert.That(clientId.Bytes.ToArray()).IsEquivalentTo(new byte[] { 0, 0 });
        await Assert.That(clientSecret.Bytes.ToArray()).IsEquivalentTo(new byte[] { 0, 0 });
        await Assert.That(apiKey.Bytes.ToArray()).IsEquivalentTo(new byte[] { 0, 0 });
    }

    [Test]
    public async Task Module_registers_replaceable_unavailable_secret_source()
    {
        var services = new ServiceCollection();
        new IdentityModule().ConfigureServices(services);
        using var provider = services.BuildServiceProvider();
        await Assert.That(provider.GetRequiredService<IExternalProviderSecretSource>()).IsTypeOf<UnavailableExternalProviderSecretSource>();
    }

    [Test]
    public async Task Authority_service_uses_server_computed_workos_binding_and_keeps_identity_tuple_immutable()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(options => new IdentityModule().Configure(options));
        await harness.InitializeAsync();
        var time = new StaticTimeProvider(new DateTimeOffset(2026, 7, 21, 5, 0, 0, TimeSpan.Zero));
        var service = new ExternalIdentityAuthorityService(
            harness.Session,
            new ConfigureExternalIdentityAuthorityRequestValidator(),
            time);
        var scope = new ExternalIdentityManagerScope(TenantId, SiteId);
        var request = new ConfigureExternalIdentityAuthorityRequest(
            ExternalMemberProviders.WorkOs,
            "org_123",
            "https://api.workos.com",
            11,
            "production",
            true);

        var created = Ok(await service.ConfigureAsync(scope, request));
        var stored = (await harness.Session.LoadAsync<ExternalOrganizationBinding>(created.BindingId))!;

        await Assert.That(created.BindingId).IsGreaterThan(0);
        await Assert.That(stored.CredentialPath).IsEqualTo(
            ExternalProviderSecretReference.CanonicalCredentialPath(TenantId, ExternalMemberProviders.WorkOs));
        await Assert.That(stored.Issuer).IsEqualTo("https://api.workos.com");
        await Assert.That(stored.Authority).IsEqualTo("https://api.workos.com");

        var updated = Ok(await service.ConfigureAsync(scope, request with { VaultId = 12, VaultEnvironment = "staging", Enabled = false }));
        await Assert.That(updated.BindingId).IsEqualTo(created.BindingId);

        var conflict = await service.ConfigureAsync(scope, request with { OrganizationId = "org_other" });
        await Assert.That(conflict).IsTypeOf<Result<ExternalIdentityAuthorityResult, AeroError>.Failure>();
    }

    [Test]
    [Arguments("11111111-2222-3333-4444-555555555555", "https://contoso.ciamlogin.com/11111111-2222-3333-4444-555555555555/v2.0", true)]
    [Arguments("11111111-2222-3333-4444-555555555555", "https://contoso.ciamlogin.com:444/11111111-2222-3333-4444-555555555555/v2.0", false)]
    [Arguments("11111111-2222-3333-4444-555555555555", "https://contoso.ciamlogin.com/11111111-2222-3333-4444-555555555555/v2.0/", false)]
    [Arguments("not-a-guid", "https://not-a-guid.ciamlogin.com/not-a-guid/v2.0", false)]
    [Arguments("11111111-2222-3333-4444-AAAAAAAAAAAA", "https://contoso.ciamlogin.com/11111111-2222-3333-4444-aaaaaaaaaaaa/v2.0", false)]
    [Arguments("11111111-2222-3333-4444-555555555555", "https://xn--contoso-q0a.ciamlogin.com/11111111-2222-3333-4444-555555555555/v2.0", false)]
    [Arguments("11111111-2222-3333-4444-555555555555", "https://contoso.ciamlogin.com/common/v2.0", false)]
    [Arguments("11111111-2222-3333-4444-555555555555", "https://login.microsoftonline.com/11111111-2222-3333-4444-555555555555/v2.0", false)]
    public async Task Authority_service_enforces_exact_entra_host_and_path(string organizationId, string authority, bool expectedSuccess)
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(options => new IdentityModule().Configure(options));
        await harness.InitializeAsync();
        var service = new ExternalIdentityAuthorityService(
            harness.Session,
            new ConfigureExternalIdentityAuthorityRequestValidator(),
            TimeProvider.System);

        var result = await service.ConfigureAsync(
            new(TenantId, SiteId),
            new(ExternalMemberProviders.EntraExternalId, organizationId, authority, 1, "production", true));

        await Assert.That(result is Result<ExternalIdentityAuthorityResult, AeroError>.Ok).IsEqualTo(expectedSuccess);
    }

    private static BeginExternalMemberSignInRequest Request() => new(TenantId, SiteId, BindingId, null,
        ExternalMemberProviders.WorkOs, "/shop/checkout", "sealed-correlation");

    private static async Task<Setup> CreateSetupAsync()
    {
        var time = new StaticTimeProvider(new DateTimeOffset(2026, 7, 21, 5, 0, 0, TimeSpan.Zero));
        var harness = new SableTestHarness()
            .WithSchema<SitesModel>(SchemaMode.Flexible)
            .WithConfiguration(options => new IdentityModule().Configure(options));
        await harness.InitializeAsync();
        harness.Session.Store(new SitesModel { Id = SiteId, TenantId = TenantId, Name = "Store", IsEnabled = true });
        harness.Session.Store(new ExternalOrganizationBinding
        {
            Id = BindingId, TenantId = TenantId, Provider = ExternalMemberProviders.WorkOs, Issuer = "https://issuer.example.com",
            OrganizationId = "org_123", BindingKey = Key(ExternalMemberProviders.WorkOs, "https://issuer.example.com", "org_123"), Authority = "https://authority.example.com", VaultId = 1,
            VaultEnvironment = "production", CredentialPath = ExternalProviderSecretReference.CanonicalCredentialPath(TenantId, ExternalMemberProviders.WorkOs), IsActive = true
        });
        await harness.Session.SaveChangesAsync();
        return new(harness, new ExternalMemberIssuanceService(harness.Session,
            new CreateExternalMemberInvitationRequestValidator(time), new BeginExternalMemberSignInRequestValidator(),
            new CompleteExternalMemberSignInRequestValidator(time), time), time);
    }

    private static T Ok<T>(Result<T, AeroError> result) => result is Result<T, AeroError>.Ok(var value)
        ? value : throw new InvalidOperationException("Expected success.");
    private static long StateId(string handle) => long.Parse(handle.AsSpan(0, handle.IndexOf('.')), System.Globalization.CultureInfo.InvariantCulture);

    private static string FlipLastHandleCharacter(string handle) =>
        handle[..^1] + (handle[^1] == 'a' ? "b" : "a");
    private static string Key(params string[] values)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (var value in values)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
        return $"v1.{Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(hash.GetHashAndReset())}";
    }
    private sealed record Setup(SableTestHarness Harness, ExternalMemberIssuanceService Service, StaticTimeProvider Time) : IAsyncDisposable { public ValueTask DisposeAsync() => Harness.DisposeAsync(); }
    private sealed class StaticTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
