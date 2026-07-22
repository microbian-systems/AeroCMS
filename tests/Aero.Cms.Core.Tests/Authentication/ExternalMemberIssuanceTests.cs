using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Identity;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Models.Entities;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ExternalMemberIssuanceTests
{
    private const long TenantId = 4101;
    private const long SiteId = 4201;
    private const long OtherSiteId = 4202;
    private const long BindingId = 4301;
    private const string Provider = "workos";
    private const string Issuer = "https://issuer.example.com";
    private const string Organization = "org_exact_A1";
    private const string Email = "Shopper@Example.com";

    [Test]
    public async Task Invitation_and_state_use_opaque_handles_and_persist_only_digests()
    {
        await using var setup = await CreateSetupAsync();

        var invitation = Ok(await setup.Service.CreateInvitationAsync(InvitationRequest(setup)));
        var state = Ok(await setup.Service.BeginAsync(BeginRequest(invitation.Handle)));
        var storedInvitation = await setup.Harness.Session.LoadAsync<ExternalMemberInvitation>(HandleId(invitation.Handle));
        var storedState = await setup.Harness.Session.LoadAsync<ExternalAuthenticationState>(HandleId(state.Handle));

        AssertCanonicalHandle(invitation.Handle);
        AssertCanonicalHandle(state.Handle);
        await Assert.That(storedInvitation).IsNotNull();
        await Assert.That(invitation.Handle).DoesNotContain(storedInvitation!.TokenDigest);
        await Assert.That(storedInvitation.TokenDigest.Length).IsEqualTo(64);
        await Assert.That(storedInvitation.NormalizedEmail).IsEqualTo("shopper@example.com");
        await Assert.That(storedState).IsNotNull();
        await Assert.That(state.Handle).DoesNotContain(storedState!.SecretDigest);
        await Assert.That(storedState.SecretDigest.Length).IsEqualTo(64);
    }

    [Test]
    [Arguments("//evil.example/path")]
    [Arguments("/\\evil")]
    [Arguments("https://evil.example/path")]
    public async Task Begin_rejects_unsafe_return_paths(string returnPath)
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Service.CreateInvitationAsync(InvitationRequest(setup)));

        var result = await setup.Service.BeginAsync(BeginRequest(invitation.Handle) with { ReturnPath = returnPath });

        await Assert.That(result).IsTypeOf<Result<ExternalMemberAuthenticationHandle, AeroError>.Failure>();
        await Assert.That(await setup.Harness.Session.Query<ExternalAuthenticationState>().ToListAsync()).IsEmpty();
    }

    [Test]
    public async Task Complete_commits_member_link_assignment_session_and_consumption_once()
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Service.CreateInvitationAsync(InvitationRequest(setup)));
        var state = Ok(await setup.Service.BeginAsync(BeginRequest(invitation.Handle)));
        setup.Listener.Reset();

        var receipt = Ok(await setup.Service.CompleteAsync(CompleteRequest(setup, state.Handle, Subject(setup, "one"))));

        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(1);
        var member = await setup.Harness.Session.LoadAsync<ExternalMember>(receipt.ExternalMemberId);
        var link = await setup.Harness.Session.LoadAsync<ExternalIdentityLink>(receipt.ExternalIdentityLinkId);
        var localSession = await setup.Harness.Session.LoadAsync<ExternalMemberSession>(receipt.ExternalMemberSessionId);
        var assignment = await setup.Harness.Session.Query<ExternalMemberSiteAssignment>()
            .FirstOrDefaultAsync(value => value.ExternalMemberId == receipt.ExternalMemberId && value.SiteId == SiteId);
        var persistedInvitation = await setup.Harness.Session.LoadAsync<ExternalMemberInvitation>(HandleId(invitation.Handle));
        var persistedState = await setup.Harness.Session.LoadAsync<ExternalAuthenticationState>(HandleId(state.Handle));

        await Assert.That(member).IsNotNull();
        await Assert.That(link).IsNotNull();
        await Assert.That(link!.IdentityKey.StartsWith("v1.", StringComparison.Ordinal)).IsTrue();
        await Assert.That(link.IdentityKey.Length).IsEqualTo(46);
        await Assert.That(localSession!.ExternalIdentityLinkId).IsEqualTo(link.Id);
        await Assert.That(assignment!.TenantId).IsEqualTo(TenantId);
        await Assert.That(persistedInvitation!.ConsumedByExternalMemberId).IsEqualTo(member!.Id);
        await Assert.That(persistedState!.ConsumedAt).IsNotNull();
        await Assert.That(receipt.ReturnPath).IsEqualTo("/shop/checkout");
    }

    [Test]
    public async Task Existing_linked_member_with_active_assignment_signs_in_without_invitation()
    {
        await using var setup = await CreateSetupAsync();
        var identity = Subject(setup, "returning");
        var first = await IssueAsync(setup, identity);
        var state = Ok(await setup.Service.BeginAsync(BeginRequest(invitationHandle: null)));

        var returning = Ok(await setup.Service.CompleteAsync(CompleteRequest(setup, state.Handle, identity)));

        await Assert.That(returning.ExternalMemberId).IsEqualTo(first.ExternalMemberId);
        await Assert.That(returning.ExternalIdentityLinkId).IsEqualTo(first.ExternalIdentityLinkId);
        await Assert.That(returning.ExternalMemberSessionId).IsNotEqualTo(first.ExternalMemberSessionId);
    }

    [Test]
    public async Task New_identity_without_invitation_cannot_create_member_or_link()
    {
        await using var setup = await CreateSetupAsync();
        var state = Ok(await setup.Service.BeginAsync(BeginRequest(invitationHandle: null)));

        var result = await setup.Service.CompleteAsync(CompleteRequest(setup, state.Handle, Subject(setup, "new-no-invite")));

        await Assert.That(result).IsTypeOf<Result<ExternalMemberIssuanceReceipt, AeroError>.Failure>();
        await Assert.That(await setup.Harness.Session.Query<ExternalMember>().ToListAsync()).IsEmpty();
        await Assert.That(await setup.Harness.Session.Query<ExternalIdentityLink>().ToListAsync()).IsEmpty();
        await Assert.That(await setup.Harness.Session.Query<ExternalMemberSession>().ToListAsync()).IsEmpty();
    }

    [Test]
    public async Task Existing_link_requires_an_invitation_to_create_a_missing_site_assignment()
    {
        await using var setup = await CreateSetupAsync(includeOtherSite: true);
        var identity = Subject(setup, "second-site");
        var first = await IssueAsync(setup, identity);
        var noInviteState = Ok(await setup.Service.BeginAsync(BeginRequest(null, OtherSiteId)));
        var denied = await setup.Service.CompleteAsync(CompleteRequest(setup, noInviteState.Handle, identity, OtherSiteId));
        await Assert.That(denied).IsTypeOf<Result<ExternalMemberIssuanceReceipt, AeroError>.Failure>();

        var invitation = Ok(await setup.Service.CreateInvitationAsync(InvitationRequest(setup, OtherSiteId)));
        var invitedState = Ok(await setup.Service.BeginAsync(BeginRequest(invitation.Handle, OtherSiteId)));
        var granted = Ok(await setup.Service.CompleteAsync(CompleteRequest(setup, invitedState.Handle, identity, OtherSiteId)));

        await Assert.That(granted.ExternalMemberId).IsEqualTo(first.ExternalMemberId);
        var assignment = await setup.Harness.Session.Query<ExternalMemberSiteAssignment>()
            .FirstOrDefaultAsync(value => value.ExternalMemberId == first.ExternalMemberId && value.SiteId == OtherSiteId);
        await Assert.That(assignment).IsNotNull();
        await Assert.That(assignment!.IsActive).IsTrue();
    }

    [Test]
    public async Task Unverified_email_is_rejected_when_invitation_is_required_but_not_for_returning_assignment()
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Service.CreateInvitationAsync(InvitationRequest(setup)));
        var state = Ok(await setup.Service.BeginAsync(BeginRequest(invitation.Handle)));
        var unverified = Subject(setup, "unverified") with { EmailVerified = false };
        var denied = await setup.Service.CompleteAsync(CompleteRequest(setup, state.Handle, unverified));
        await Assert.That(denied).IsTypeOf<Result<ExternalMemberIssuanceReceipt, AeroError>.Failure>();

        var linked = Subject(setup, "linked-unverified");
        var first = await IssueAsync(setup, linked);
        var returningState = Ok(await setup.Service.BeginAsync(BeginRequest(null)));
        var returning = Ok(await setup.Service.CompleteAsync(
            CompleteRequest(setup, returningState.Handle, linked with { EmailVerified = false, Email = null })));
        await Assert.That(returning.ExternalMemberId).IsEqualTo(first.ExternalMemberId);
    }

    [Test]
    [Arguments(-301)]
    [Arguments(61)]
    public async Task Complete_rejects_stale_or_future_validated_identity(int secondsOffset)
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Service.CreateInvitationAsync(InvitationRequest(setup)));
        var state = Ok(await setup.Service.BeginAsync(BeginRequest(invitation.Handle)));
        var identity = Subject(setup, "freshness") with { ValidatedAt = setup.Time.GetUtcNow().AddSeconds(secondsOffset) };

        var result = await setup.Service.CompleteAsync(CompleteRequest(setup, state.Handle, identity));

        await Assert.That(result).IsTypeOf<Result<ExternalMemberIssuanceReceipt, AeroError>.Failure>();
        await Assert.That(await setup.Harness.Session.Query<ExternalMemberSession>().ToListAsync()).IsEmpty();
    }

    [Test]
    [Arguments("local_identity", "https://issuer.example.com")]
    [Arguments("workos", "https://user@issuer.example.com")]
    public async Task Reserved_provider_and_issuer_userinfo_are_rejected(string provider, string issuer)
    {
        await using var setup = await CreateSetupAsync();
        var state = Ok(await setup.Service.BeginAsync(BeginRequest(null)));
        var request = CompleteRequest(setup, state.Handle, Subject(setup, "reserved") with { Provider = provider, Issuer = issuer })
            with { Provider = provider };

        var result = await setup.Service.CompleteAsync(request);

        await Assert.That(result).IsTypeOf<Result<ExternalMemberIssuanceReceipt, AeroError>.Failure>();
    }

    [Test]
    [Arguments("")]
    [Arguments("1")]
    [Arguments("1.short")]
    [Arguments("1.aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.extra")]
    [Arguments("0.aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [Arguments("01.aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [Arguments("1.!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")]
    public async Task Malformed_or_noncanonical_handles_are_rejected_without_disclosure(string handle)
    {
        await using var setup = await CreateSetupAsync();
        var result = await setup.Service.CompleteAsync(CompleteRequest(setup, handle, Subject(setup, "malformed")));

        var failure = result as Result<ExternalMemberIssuanceReceipt, AeroError>.Failure;
        await Assert.That(failure).IsNotNull();
        if (handle.Length >= 8)
            await Assert.That(failure!.Error.ToString()).DoesNotContain(handle);
    }

    [Test]
    public async Task Complete_rejects_replay_without_creating_another_session()
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Service.CreateInvitationAsync(InvitationRequest(setup)));
        var state = Ok(await setup.Service.BeginAsync(BeginRequest(invitation.Handle)));
        var request = CompleteRequest(setup, state.Handle, Subject(setup, "replay"));
        _ = Ok(await setup.Service.CompleteAsync(request));

        var replay = await setup.Service.CompleteAsync(request);

        await Assert.That(replay).IsTypeOf<Result<ExternalMemberIssuanceReceipt, AeroError>.Failure>();
        await Assert.That((await setup.Harness.Session.Query<ExternalMemberSession>().ToListAsync()).Count).IsEqualTo(1);
    }

    [Test]
    [Arguments("tenant")]
    [Arguments("site")]
    [Arguments("provider")]
    [Arguments("issuer")]
    [Arguments("organization")]
    public async Task Complete_rejects_callback_mix_up(string field)
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Service.CreateInvitationAsync(InvitationRequest(setup)));
        var state = Ok(await setup.Service.BeginAsync(BeginRequest(invitation.Handle)));
        var request = CompleteRequest(setup, state.Handle, Subject(setup, "mix-up"));
        request = field switch
        {
            "tenant" => request with { TenantId = TenantId + 1 },
            "site" => request with { SiteId = SiteId + 1 },
            "provider" => request with { Provider = "entra_external_id" },
            "issuer" => request with { Identity = request.Identity with { Issuer = "https://other.example.com" } },
            _ => request with { Identity = request.Identity with { OrganizationId = "org_other" } }
        };

        var result = await setup.Service.CompleteAsync(request);

        await Assert.That(result).IsTypeOf<Result<ExternalMemberIssuanceReceipt, AeroError>.Failure>();
        await Assert.That(await setup.Harness.Session.Query<ExternalMemberSession>().ToListAsync()).IsEmpty();
    }

    [Test]
    public async Task Same_email_with_different_subject_never_auto_links_accounts()
    {
        await using var setup = await CreateSetupAsync();
        var first = await IssueAsync(setup, Subject(setup, "first"));
        var second = await IssueAsync(setup, Subject(setup, "second"));

        await Assert.That(second.ExternalMemberId).IsNotEqualTo(first.ExternalMemberId);
        await Assert.That((await setup.Harness.Session.Query<ExternalMember>().ToListAsync()).Count).IsEqualTo(2);
        await Assert.That((await setup.Harness.Session.Query<ExternalIdentityLink>().ToListAsync()).Count).IsEqualTo(2);
    }

    [Test]
    public async Task Existing_inactive_assignment_is_never_reactivated()
    {
        await using var setup = await CreateSetupAsync();
        var identity = Subject(setup, "inactive-assignment");
        var first = await IssueAsync(setup, identity);
        var assignment = await setup.Harness.Session.Query<ExternalMemberSiteAssignment>()
            .FirstOrDefaultAsync(value => value.ExternalMemberId == first.ExternalMemberId && value.SiteId == SiteId);
        assignment!.IsActive = false;
        setup.Harness.Session.Store(assignment);
        await setup.Harness.Session.SaveChangesAsync();
        var invitation = Ok(await setup.Service.CreateInvitationAsync(InvitationRequest(setup)));
        var state = Ok(await setup.Service.BeginAsync(BeginRequest(invitation.Handle)));

        var result = await setup.Service.CompleteAsync(CompleteRequest(setup, state.Handle, identity));

        await Assert.That(result).IsTypeOf<Result<ExternalMemberIssuanceReceipt, AeroError>.Failure>();
        await Assert.That((await setup.Harness.Session.Query<ExternalMemberSession>().ToListAsync()).Count).IsEqualTo(1);
        await Assert.That((await setup.Harness.Session.LoadAsync<ExternalMemberSiteAssignment>(assignment.Id))!.IsActive).IsFalse();
    }

    [Test]
    public async Task Concurrent_completion_of_same_state_produces_exactly_one_receipt()
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Service.CreateInvitationAsync(InvitationRequest(setup)));
        var state = Ok(await setup.Service.BeginAsync(BeginRequest(invitation.Handle)));
        var request = CompleteRequest(setup, state.Handle, Subject(setup, "same-state-race"));
        await using var firstSession = await setup.Harness.OpenSessionAsync();
        await using var secondSession = await setup.Harness.OpenSessionAsync();

        var results = await Task.WhenAll(
            CreateService(firstSession, setup.Time).CompleteAsync(request),
            CreateService(secondSession, setup.Time).CompleteAsync(request));

        await Assert.That(results.Count(result => result is Result<ExternalMemberIssuanceReceipt, AeroError>.Ok)).IsEqualTo(1);
        await Assert.That(results.Count(result => result is Result<ExternalMemberIssuanceReceipt, AeroError>.Failure)).IsEqualTo(1);
        await using var verification = await setup.Harness.OpenSessionAsync();
        await Assert.That((await verification.Query<ExternalMemberSession>().ToListAsync()).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Concurrent_distinct_invites_for_same_identity_leave_no_orphan_principals()
    {
        await using var setup = await CreateSetupAsync();
        var firstInvite = Ok(await setup.Service.CreateInvitationAsync(InvitationRequest(setup)));
        var secondInvite = Ok(await setup.Service.CreateInvitationAsync(InvitationRequest(setup)));
        var firstState = Ok(await setup.Service.BeginAsync(BeginRequest(firstInvite.Handle)));
        var secondState = Ok(await setup.Service.BeginAsync(BeginRequest(secondInvite.Handle)));
        var identity = Subject(setup, "distinct-state-race");
        await using var firstSession = await setup.Harness.OpenSessionAsync();
        await using var secondSession = await setup.Harness.OpenSessionAsync();

        _ = await Task.WhenAll(
            CreateService(firstSession, setup.Time).CompleteAsync(CompleteRequest(setup, firstState.Handle, identity)),
            CreateService(secondSession, setup.Time).CompleteAsync(CompleteRequest(setup, secondState.Handle, identity)));

        await using var verification = await setup.Harness.OpenSessionAsync();
        var members = await verification.Query<ExternalMember>().ToListAsync();
        var links = await verification.Query<ExternalIdentityLink>().ToListAsync();
        var assignments = await verification.Query<ExternalMemberSiteAssignment>().ToListAsync();
        var sessions = await verification.Query<ExternalMemberSession>().ToListAsync();
        await Assert.That(members.Count).IsEqualTo(1);
        await Assert.That(links.Count).IsEqualTo(1);
        await Assert.That(assignments.Count).IsEqualTo(1);
        await Assert.That(sessions.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(sessions.All(value => value.ExternalMemberId == members[0].Id && value.ExternalIdentityLinkId == links[0].Id)).IsTrue();
    }

    [Test]
    [Arguments("concurrency")]
    [Arguments("unique")]
    [Arguments("generic")]
    public async Task Commit_failures_return_typed_sanitized_errors_and_leave_no_artifacts(string scenario)
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Service.CreateInvitationAsync(InvitationRequest(setup)));
        var state = Ok(await setup.Service.BeginAsync(BeginRequest(invitation.Handle)));
        setup.Listener.Reset();
        setup.Listener.Exception = scenario switch
        {
            "concurrency" => new ConcurrencyException(typeof(ExternalAuthenticationState), HandleId(state.Handle), 1, 2),
            "unique" => new InvalidOperationException("unique index constraint collision SECRET-DETAIL"),
            _ => new InvalidOperationException("database transport SECRET-DETAIL")
        };

        var result = await setup.Service.CompleteAsync(CompleteRequest(setup, state.Handle, Subject(setup, "commit-failure")));
        setup.Listener.Exception = null;
        var failure = (Result<ExternalMemberIssuanceReceipt, AeroError>.Failure)result;

        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(1);
        if (scenario is "concurrency" or "unique")
            await Assert.That(failure.Error).IsTypeOf<AeroError.Conflict>();
        else
            await Assert.That(failure.Error).IsTypeOf<AeroError.Database>();
        await Assert.That(failure.Error.ToString()).DoesNotContain("SECRET-DETAIL");

        await using var verification = await setup.Harness.OpenSessionAsync();
        await Assert.That(await verification.Query<ExternalMember>().ToListAsync()).IsEmpty();
        await Assert.That(await verification.Query<ExternalIdentityLink>().ToListAsync()).IsEmpty();
        await Assert.That(await verification.Query<ExternalMemberSession>().ToListAsync()).IsEmpty();
        await Assert.That(await verification.Query<ExternalMemberSiteAssignment>().ToListAsync()).IsEmpty();
        await Assert.That((await verification.LoadAsync<ExternalAuthenticationState>(HandleId(state.Handle)))!.ConsumedAt).IsNull();
        await Assert.That((await verification.LoadAsync<ExternalMemberInvitation>(HandleId(invitation.Handle)))!.ConsumedAt).IsNull();
    }

    [Test]
    public async Task Cancellation_during_validation_returns_cancelled_and_writes_nothing()
    {
        await using var setup = await CreateSetupAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await setup.Service.CreateInvitationAsync(InvitationRequest(setup), cancellation.Token);

        await Assert.That(((Result<ExternalMemberInvitationHandle, AeroError>.Failure)result).Error)
            .IsTypeOf<AeroError.Cancelled>();
        await Assert.That(await setup.Harness.Session.Query<ExternalMemberInvitation>().ToListAsync()).IsEmpty();
    }

    [Test]
    public async Task Identity_module_does_not_change_default_authentication_schemes()
    {
        var services = new ServiceCollection();
        new IdentityModule().ConfigureServices(services);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        await Assert.That(options.DefaultScheme).IsNull();
        await Assert.That(options.DefaultAuthenticateScheme).IsNull();
        await Assert.That(options.DefaultChallengeScheme).IsNull();
        await Assert.That(ExternalMemberAuthenticationDefaults.Scheme).IsNotEqualTo("Identity.Application");
    }

    [Test]
    public async Task Service_contract_has_no_http_or_authentication_dependency()
    {
        var parameterTypes = typeof(ExternalMemberIssuanceService).GetConstructors().Single()
            .GetParameters().Select(parameter => parameter.ParameterType).ToArray();
        await Assert.That(parameterTypes.Any(type => type.FullName is
            "Microsoft.AspNetCore.Http.IHttpContextAccessor" or
            "Microsoft.AspNetCore.Authentication.IAuthenticationService" or
            "System.Security.Claims.ClaimsPrincipal")).IsFalse();
    }

    private static async Task<ExternalMemberIssuanceReceipt> IssueAsync(Setup setup, ValidatedExternalIdentity identity)
    {
        var invitation = Ok(await setup.Service.CreateInvitationAsync(InvitationRequest(setup)));
        var state = Ok(await setup.Service.BeginAsync(BeginRequest(invitation.Handle)));
        return Ok(await setup.Service.CompleteAsync(CompleteRequest(setup, state.Handle, identity)));
    }

    private static CreateExternalMemberInvitationRequest InvitationRequest(Setup setup, long siteId = SiteId) =>
        new(TenantId, siteId, BindingId, Provider, Email, setup.Time.GetUtcNow().AddHours(1));

    private static BeginExternalMemberSignInRequest BeginRequest(string? invitationHandle, long siteId = SiteId) =>
        new(TenantId, siteId, BindingId, invitationHandle, Provider, "/shop/checkout", "protected-correlation");

    private static CompleteExternalMemberSignInRequest CompleteRequest(
        Setup setup,
        string handle,
        ValidatedExternalIdentity identity,
        long siteId = SiteId) =>
        new(handle, TenantId, siteId, Provider, identity);

    private static ValidatedExternalIdentity Subject(Setup setup, string suffix) =>
        new(Provider, Issuer, $"subject-{suffix}", Organization, Email, true, "Shopper", "provider-session-1", setup.Time.GetUtcNow());

    private static T Ok<T>(Result<T, AeroError> result) =>
        result is Result<T, AeroError>.Ok(var value)
            ? value
            : throw new InvalidOperationException($"Expected success but received {((Result<T, AeroError>.Failure)result).Error}.");

    private static async Task<Setup> CreateSetupAsync(bool includeOtherSite = false)
    {
        var listener = new CommitFaultListener();
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 7, 21, 5, 0, 0, TimeSpan.Zero));
        var harness = await CreateEmptyHarnessAsync(listener);
        harness.Session.Store(Site(SiteId));
        harness.Session.Store(Binding(BindingId, TenantId));
        if (includeOtherSite) harness.Session.Store(Site(OtherSiteId));
        await harness.Session.SaveChangesAsync();
        listener.Reset();
        return new Setup(harness, CreateService(harness.Session, time), time, listener);
    }

    private static async Task<SableTestHarness> CreateEmptyHarnessAsync(CommitFaultListener? listener = null)
    {
        var harness = new SableTestHarness()
            .WithSchema<SitesModel>(SchemaMode.Flexible)
            .WithConfiguration(options =>
            {
                new IdentityModule().Configure(options);
                if (listener is not null) options.Listeners.Add(listener);
            });
        await harness.InitializeAsync();
        return harness;
    }

    private static ExternalMemberIssuanceService CreateService(IDocumentSession session, TimeProvider timeProvider) =>
        new(
            session,
            new CreateExternalMemberInvitationRequestValidator(timeProvider),
            new BeginExternalMemberSignInRequestValidator(),
            new CompleteExternalMemberSignInRequestValidator(timeProvider),
            timeProvider);

    private static SitesModel Site(long id) => new() { Id = id, TenantId = TenantId, Name = "Store", IsEnabled = true };

    private static ExternalOrganizationBinding Binding(long id, long tenantId, string organization = Organization) => new()
    {
        Id = id,
        TenantId = tenantId,
        Provider = Provider,
        Issuer = Issuer,
        OrganizationId = organization,
        BindingKey = Key(Provider, Issuer, organization),
        Authority = "https://authority.example.com",
        VaultId = 9001,
        VaultEnvironment = "production",
        CredentialPath = ExternalProviderSecretReference.CanonicalCredentialPath(tenantId, Provider),
        IsActive = true
    };

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

    private static long HandleId(string handle) => long.Parse(handle.AsSpan(0, handle.IndexOf('.')), System.Globalization.CultureInfo.InvariantCulture);

    private static void AssertCanonicalHandle(string handle)
    {
        var parts = handle.Split('.');
        if (parts.Length != 2 || !long.TryParse(parts[0], out var id) || id <= 0 || parts[1].Length != 43 ||
            Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(parts[1]).Length != 32)
            throw new InvalidOperationException("Expected canonical opaque handle.");
    }

    private sealed record Setup(
        SableTestHarness Harness,
        ExternalMemberIssuanceService Service,
        MutableTimeProvider Time,
        CommitFaultListener Listener) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Harness.DisposeAsync();
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class CommitFaultListener : IDocumentSessionListener
    {
        public int SaveCalls { get; private set; }
        public Exception? Exception { get; set; }

        public Task BeforeSaveChangesAsync(IDocumentSession session, CancellationToken ct)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }

        public Task BeforeCommitAsync(IDocumentSession session, CancellationToken ct) =>
            Exception is null ? Task.CompletedTask : Task.FromException(Exception);

        public void Reset()
        {
            SaveCalls = 0;
            Exception = null;
        }
    }
}
