using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Identity;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ExternalMemberAuthenticationCoordinatorTests
{
    private const long TenantId = 711;
    private const long SiteId = 712;
    private const long BindingId = 713;
    private static readonly ExternalMemberTrustedRoute Route = new(new Uri("https://store.example.com/auth/callback"), "store.example.com");

    [Test]
    public async Task Factory_selects_exact_provider_and_fails_closed_for_null_unknown_or_duplicates()
    {
        var workOs = new RecordingStrategy(ExternalMemberProviders.WorkOs);
        var entra = new RecordingStrategy(ExternalMemberProviders.EntraExternalId);
        var factory = new ExternalMemberProviderStrategyFactory([workOs, entra]);

        await Assert.That(Ok(factory.Resolve(ExternalMemberProviders.WorkOs))).IsSameReferenceAs(workOs);
        await Assert.That(factory.Resolve(null)).IsTypeOf<Result<IExternalMemberProviderStrategy, AeroError>.Failure>();
        await Assert.That(factory.Resolve("WORKOS")).IsTypeOf<Result<IExternalMemberProviderStrategy, AeroError>.Failure>();
        await Assert.That(new ExternalMemberProviderStrategyFactory([workOs, new RecordingStrategy(ExternalMemberProviders.WorkOs)])
            .Resolve(ExternalMemberProviders.WorkOs)).IsTypeOf<Result<IExternalMemberProviderStrategy, AeroError>.Failure>();
    }

    [Test]
    [Arguments(false, TenantId)]
    [Arguments(true, TenantId + 1)]
    public async Task Begin_rejects_disabled_or_tenant_mismatched_site_before_secrets_or_strategy(bool enabled, long storedTenant)
    {
        await using var setup = await SetupAsync(enabled, storedTenant);
        var result = await setup.Coordinator.BeginAsync(new(null, "/store"), Route);

        await Assert.That(result).IsTypeOf<Result<ExternalMemberAuthenticationBeginResult, AeroError>.Failure>();
        await Assert.That(setup.Secrets.Reads).IsEqualTo(0);
        await Assert.That(setup.WorkOs.Calls).IsEqualTo(0);
    }

    [Test]
    public async Task Begin_reads_once_then_uses_only_selected_provider_and_clears_credentials_and_passes_committed_handle()
    {
        await using var setup = await SetupAsync();
        var result = Ok(await setup.Coordinator.BeginAsync(new(null, "/store"), Route));

        await Assert.That(setup.Secrets.Reads).IsEqualTo(1);
        await Assert.That(setup.WorkOs.PrepareCalls).IsEqualTo(1);
        await Assert.That(setup.WorkOs.CreateCalls).IsEqualTo(1);
        await Assert.That(setup.Entra.Calls).IsEqualTo(0);
        await Assert.That(setup.WorkOs.LastHandle).IsEqualTo(result.Handle.Handle);
        await Assert.That(setup.WorkOs.ClientId!.Value.ToArray()).IsEquivalentTo(new byte[] { 0, 0 });
        await Assert.That(setup.WorkOs.ClientSecret!.Value.ToArray()).IsEquivalentTo(new byte[] { 0, 0 });
    }

    [Test]
    public async Task Begin_secret_or_provider_failure_never_leaks_credentials_or_reaches_later_strategy_step()
    {
        await using var secretFailure = await SetupAsync();
        secretFailure.Secrets.Fail = true;
        await Assert.That(await secretFailure.Coordinator.BeginAsync(new(null, "/store"), Route))
            .IsTypeOf<Result<ExternalMemberAuthenticationBeginResult, AeroError>.Failure>();
        await Assert.That(secretFailure.Secrets.Reads).IsEqualTo(1);
        await Assert.That(secretFailure.WorkOs.Calls).IsEqualTo(0);

        await using var providerFailure = await SetupAsync();
        providerFailure.WorkOs.FailPrepare = true;
        await Assert.That(await providerFailure.Coordinator.BeginAsync(new(null, "/store"), Route))
            .IsTypeOf<Result<ExternalMemberAuthenticationBeginResult, AeroError>.Failure>();
        await Assert.That(providerFailure.WorkOs.ClientId!.Value.ToArray()).IsEquivalentTo(new byte[] { 0, 0 });
    }

    [Test]
    public async Task Callback_prepares_before_any_secret_or_provider_call_and_valid_state_authenticates_then_completes()
    {
        await using var forged = await SetupAsync();
        forged.Issuance.PrepareSucceeds = false;
        await Assert.That(await forged.Coordinator.CallbackAsync("forged", Route, "code", null))
            .IsTypeOf<Result<ExternalMemberAuthenticationCallbackResult, AeroError>.Failure>();
        await Assert.That(forged.Issuance.Events).IsEquivalentTo(["prepare"]);
        await Assert.That(forged.Secrets.Reads).IsEqualTo(0);
        await Assert.That(forged.WorkOs.Calls).IsEqualTo(0);

        await using var valid = await SetupAsync();
        var callback = Ok(await valid.Coordinator.CallbackAsync("state.handle", Route, "code", null));
        await Assert.That(callback.Identity.Provider).IsEqualTo(ExternalMemberProviders.WorkOs);
        await Assert.That(valid.Issuance.Events).IsEquivalentTo(["prepare", "complete"]);
        await Assert.That(valid.Secrets.Reads).IsEqualTo(1);
        await Assert.That(valid.WorkOs.AuthenticateCalls).IsEqualTo(1);
        await Assert.That(valid.WorkOs.ClientId!.Value.ToArray()).IsEquivalentTo(new byte[] { 0, 0 });
    }

    [Test]
    public async Task Begin_rejects_untrusted_route_before_secrets_or_provider()
    {
        await using var setup = await SetupAsync();
        var bad = new ExternalMemberTrustedRoute(new Uri("https://store.example.com:444/auth/callback?q=x"), "store.example.com");
        await Assert.That(await setup.Coordinator.BeginAsync(new(null, "/store"), bad))
            .IsTypeOf<Result<ExternalMemberAuthenticationBeginResult, AeroError>.Failure>();
        await Assert.That(setup.Secrets.Reads).IsEqualTo(0);
        await Assert.That(setup.WorkOs.Calls).IsEqualTo(0);
    }

    private static async Task<Setup> SetupAsync(bool enabled = true, long storedTenant = TenantId)
    {
        var harness = new SableTestHarness().WithSchema<SitesModel>(SchemaMode.Flexible)
            .WithConfiguration(options => new IdentityModule().Configure(options));
        await harness.InitializeAsync();
        harness.Session.Store(new SitesModel { Id = SiteId, TenantId = storedTenant, Name = "Store", IsEnabled = enabled });
        harness.Session.Store(Binding());
        await harness.Session.SaveChangesAsync();
        var site = Substitute.For<ISiteContext>(); site.SiteId.Returns(SiteId); site.TenantId.Returns(TenantId);
        var issuance = new RecordingIssuance(); var workOs = new RecordingStrategy(ExternalMemberProviders.WorkOs); var entra = new RecordingStrategy(ExternalMemberProviders.EntraExternalId); var secrets = new RecordingSecrets();
        return new(harness, new ExternalMemberAuthenticationCoordinator(site, harness.Session, issuance, new ExternalMemberProviderStrategyFactory([workOs, entra]), secrets), issuance, secrets, workOs, entra);
    }

    private static ExternalOrganizationBinding Binding() => new() { Id = BindingId, TenantId = TenantId, Provider = ExternalMemberProviders.WorkOs, Issuer = "https://api.workos.com", OrganizationId = "org_123", Authority = "https://api.workos.com", VaultId = 1, VaultEnvironment = "production", CredentialPath = ExternalProviderSecretReference.CanonicalCredentialPath(TenantId, ExternalMemberProviders.WorkOs), IsActive = true, BindingKey = Key(ExternalMemberProviders.WorkOs, "https://api.workos.com", "org_123") };
    private static string Key(params string[] values)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (var value in values) { var bytes = System.Text.Encoding.UTF8.GetBytes(value); System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length); hash.AppendData(length); hash.AppendData(bytes); }
        return "v1." + Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(hash.GetHashAndReset());
    }
    private static T Ok<T>(Result<T, AeroError> value) => value is Result<T, AeroError>.Ok(var result) ? result : throw new InvalidOperationException("Expected success.");
    private sealed record Setup(SableTestHarness Harness, ExternalMemberAuthenticationCoordinator Coordinator, RecordingIssuance Issuance, RecordingSecrets Secrets, RecordingStrategy WorkOs, RecordingStrategy Entra) : IAsyncDisposable { public ValueTask DisposeAsync() => Harness.DisposeAsync(); }

    private sealed class RecordingSecrets : IExternalProviderSecretSource
    {
        public int Reads; public bool Fail;
        public Task<Result<ExternalProviderCredentialBundle, AeroError>> ReadAsync(ExternalProviderSecretReference reference, CancellationToken cancellationToken = default) => Task.FromResult(++Reads > 0 && !Fail ? Prelude.Ok<ExternalProviderCredentialBundle, AeroError>(new([1, 2], [3, 4], null)) : Prelude.Fail<ExternalProviderCredentialBundle, AeroError>(AeroError.CreateError("unavailable")));
    }
    private sealed class RecordingStrategy(string provider) : IExternalMemberProviderStrategy
    {
        public string Provider { get; } = provider; public int PrepareCalls; public int CreateCalls; public int AuthenticateCalls; public int Calls => PrepareCalls + CreateCalls + AuthenticateCalls; public bool FailPrepare; public string? LastHandle; public ReadOnlyMemory<byte>? ClientId; public ReadOnlyMemory<byte>? ClientSecret;
        public Task<Result<ExternalProviderAuthorizationPreparation, AeroError>> PrepareAuthorizationAsync(ExternalProviderBeginContext context, ExternalProviderCredentialBundle credentials, CancellationToken cancellationToken = default) { PrepareCalls++; Capture(credentials); return Task.FromResult(FailPrepare ? Prelude.Fail<ExternalProviderAuthorizationPreparation, AeroError>(AeroError.CreateError("no")) : Prelude.Ok<ExternalProviderAuthorizationPreparation, AeroError>(new("sealed"))); }
        public Task<Result<ExternalProviderAuthorizationChallenge, AeroError>> CreateAuthorizationAsync(ExternalProviderBeginContext context, ExternalProviderAuthorizationPreparation preparation, string authenticationHandle, ExternalProviderCredentialBundle credentials, CancellationToken cancellationToken = default) { CreateCalls++; LastHandle = authenticationHandle; Capture(credentials); return Task.FromResult(Prelude.Ok<ExternalProviderAuthorizationChallenge, AeroError>(new(ExternalProviderAuthorizationChallengeKind.Redirect, "https://api.workos.com", new Dictionary<string, string>()))); }
        public Task<Result<ValidatedExternalIdentity, AeroError>> AuthenticateAsync(ExternalProviderCallbackContext context, ExternalProviderCredentialBundle credentials, CancellationToken cancellationToken = default) { AuthenticateCalls++; Capture(credentials); return Task.FromResult(Prelude.Ok<ValidatedExternalIdentity, AeroError>(new(Provider, context.Authority.Issuer, "subject", context.Authority.OrganizationId, "x@example.com", true, null, null, DateTimeOffset.UtcNow))); }
        public Task<Result<ExternalProviderAuthorizationChallenge, AeroError>> PrepareLogoutAsync(ExternalProviderLogoutContext context, ExternalProviderCredentialBundle credentials, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        private void Capture(ExternalProviderCredentialBundle credentials) { ClientId = credentials.LeaseClientId().Bytes; ClientSecret = credentials.LeaseClientSecret().Bytes; }
    }
    private sealed class RecordingIssuance : IExternalMemberIssuanceService
    {
        public bool PrepareSucceeds = true; public List<string> Events { get; } = [];
        public Task<Result<ExternalMemberInvitationHandle, AeroError>> CreateInvitationAsync(CreateExternalMemberInvitationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<ExternalMemberAuthenticationHandle, AeroError>> BeginAsync(BeginExternalMemberSignInRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Prelude.Ok<ExternalMemberAuthenticationHandle, AeroError>(new("123.handle", request.ReturnPath, DateTimeOffset.UtcNow.AddMinutes(5))));
        public Task<Result<ExternalMemberCallbackPreparation, AeroError>> PrepareCallbackAsync(string authenticationHandle, long expectedTenantId, long expectedSiteId, string expectedProvider, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<ExternalMemberCallbackPreparationWithProvider, AeroError>> PrepareCallbackAsync(string authenticationHandle, long expectedTenantId, long expectedSiteId, CancellationToken cancellationToken = default) { Events.Add("prepare"); return Task.FromResult(PrepareSucceeds ? Prelude.Ok<ExternalMemberCallbackPreparationWithProvider, AeroError>(new(BindingId, ExternalMemberProviders.WorkOs, "sealed", "/store")) : Prelude.Fail<ExternalMemberCallbackPreparationWithProvider, AeroError>(AeroError.CreateError("bad"))); }
        public Task<Result<ExternalMemberIssuanceReceipt, AeroError>> CompleteAsync(CompleteExternalMemberSignInRequest request, CancellationToken cancellationToken = default) { Events.Add("complete"); return Task.FromResult(Prelude.Ok<ExternalMemberIssuanceReceipt, AeroError>(new(1, 2, 3, TenantId, SiteId, request.Provider, 1, DateTimeOffset.UtcNow.AddHours(1), "/store"))); }
    }
}
