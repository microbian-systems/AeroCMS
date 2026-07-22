using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Identity;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Models.Entities;
using AeroDB.Sable;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class LocalExternalMemberAuthenticationTests
{
    private const long TenantId = 8101;
    private const long SiteId = 8201;
    private const long AuthorityId = 8301;
    private const long RemoteBindingId = 8401;
    private const string Email = "Shopper@Example.com";
    private const string Password = "correct horse battery staple";

    [Test]
    public async Task Activation_commits_hash_only_local_identity_graph_once()
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Local.CreateInvitationAsync(LocalInvitation(setup)));
        setup.Listener.Reset();

        var receipt = Ok(await setup.Local.ActivateInvitationAsync(Activation(invitation.Handle)));

        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(1);
        await Assert.That(receipt.Provider).IsEqualTo(LocalExternalMemberAuthentication.Provider);
        var member = await setup.Harness.Session.LoadAsync<ExternalMember>(receipt.ExternalMemberId);
        var credential = await setup.Harness.Session.Query<ExternalMemberLocalCredential>()
            .FirstOrDefaultAsync(value => value.ExternalMemberId == receipt.ExternalMemberId);
        var link = await setup.Harness.Session.LoadAsync<ExternalIdentityLink>(receipt.ExternalIdentityLinkId);
        var assignment = await setup.Harness.Session.Query<ExternalMemberSiteAssignment>()
            .FirstOrDefaultAsync(value => value.ExternalMemberId == receipt.ExternalMemberId && value.SiteId == SiteId);
        var localSession = await setup.Harness.Session.LoadAsync<ExternalMemberSession>(receipt.ExternalMemberSessionId);
        var persistedInvitation = await setup.Harness.Session.LoadAsync<ExternalMemberInvitation>(HandleId(invitation.Handle));

        await Assert.That(member).IsNotNull();
        await Assert.That(credential).IsNotNull();
        await Assert.That(credential!.PasswordHash).IsNotEqualTo(Password);
        await Assert.That(credential.PasswordHash).DoesNotContain(Password);
        await Assert.That(new PasswordHasher<ExternalMemberLocalCredential>().VerifyHashedPassword(
            credential, credential.PasswordHash, Password)).IsNotEqualTo(PasswordVerificationResult.Failed);
        await Assert.That(credential.NormalizedEmail).IsEqualTo("shopper@example.com");
        await Assert.That(link!.Provider).IsEqualTo(LocalExternalMemberAuthentication.Provider);
        await Assert.That(link.IdentityKey).IsEqualTo(Key(link.Provider, link.Issuer, link.Subject));
        await Assert.That(assignment!.TenantId).IsEqualTo(TenantId);
        await Assert.That(localSession!.AuthenticationProvider).IsEqualTo(LocalExternalMemberAuthentication.Provider);
        await Assert.That(persistedInvitation!.ConsumedByExternalMemberId).IsEqualTo(member!.Id);
    }

    [Test]
    public async Task Local_and_remote_invitations_persist_exclusive_authority_discriminators()
    {
        await using var setup = await CreateSetupAsync();

        var local = Ok(await setup.Local.CreateInvitationAsync(LocalInvitation(setup)));
        var remote = Ok(await setup.Remote.CreateInvitationAsync(new(
            TenantId, SiteId, RemoteBindingId, ExternalMemberProviders.WorkOs, Email,
            setup.Time.GetUtcNow().AddHours(1))));
        var localDocument = await setup.Harness.Session.LoadAsync<ExternalMemberInvitation>(HandleId(local.Handle));
        var remoteDocument = await setup.Harness.Session.LoadAsync<ExternalMemberInvitation>(HandleId(remote.Handle));

        await Assert.That(localDocument!.LocalAuthorityId).IsEqualTo(AuthorityId);
        await Assert.That(localDocument.OrganizationBindingId).IsNull();
        await Assert.That(remoteDocument!.OrganizationBindingId).IsEqualTo(RemoteBindingId);
        await Assert.That(remoteDocument.LocalAuthorityId).IsNull();
    }

    [Test]
    public async Task Malformed_and_forged_handles_create_no_local_principal()
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Local.CreateInvitationAsync(LocalInvitation(setup)));
        var forged = invitation.Handle[..^1] + (invitation.Handle[^1] == 'A' ? "B" : "A");

        var malformedResult = await setup.Local.ActivateInvitationAsync(Activation("1.short"));
        var forgedResult = await setup.Local.ActivateInvitationAsync(Activation(forged));

        await Assert.That(malformedResult).IsTypeOf<Result<ExternalMemberIssuanceReceipt, AeroError>.Failure>();
        await Assert.That(forgedResult).IsTypeOf<Result<ExternalMemberIssuanceReceipt, AeroError>.Failure>();
        await Assert.That(await setup.Harness.Session.Query<ExternalMember>().ToListAsync()).IsEmpty();
    }

    [Test]
    [Arguments("email")]
    [Arguments("tenant")]
    [Arguments("site")]
    [Arguments("authority")]
    public async Task Activation_rejects_scope_or_invitation_authority_mix_up(string field)
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Local.CreateInvitationAsync(LocalInvitation(setup)));
        var request = Activation(invitation.Handle);
        if (field == "email") request = request with { Email = "other@example.com" };
        if (field == "tenant") request = request with { TenantId = TenantId + 1 };
        if (field == "site") request = request with { SiteId = SiteId + 1 };
        if (field == "authority")
        {
            var document = await setup.Harness.Session.LoadAsync<ExternalMemberInvitation>(HandleId(invitation.Handle));
            document!.LocalAuthorityId = AuthorityId + 1;
            setup.Harness.Session.Store(document);
            await setup.Harness.Session.SaveChangesAsync();
        }

        var result = await setup.Local.ActivateInvitationAsync(request);

        await Assert.That(result).IsTypeOf<Result<ExternalMemberIssuanceReceipt, AeroError>.Failure>();
        await Assert.That(await setup.Harness.Session.Query<ExternalMember>().ToListAsync()).IsEmpty();
    }

    [Test]
    public async Task Expired_invitation_and_replay_are_rejected()
    {
        await using var expiredSetup = await CreateSetupAsync();
        var expired = Ok(await expiredSetup.Local.CreateInvitationAsync(LocalInvitation(expiredSetup)));
        expiredSetup.Time.Advance(TimeSpan.FromHours(2));
        await Assert.That(await expiredSetup.Local.ActivateInvitationAsync(Activation(expired.Handle)))
            .IsTypeOf<Result<ExternalMemberIssuanceReceipt, AeroError>.Failure>();

        await using var replaySetup = await CreateSetupAsync();
        var invitation = Ok(await replaySetup.Local.CreateInvitationAsync(LocalInvitation(replaySetup)));
        _ = Ok(await replaySetup.Local.ActivateInvitationAsync(Activation(invitation.Handle)));
        await Assert.That(await replaySetup.Local.ActivateInvitationAsync(Activation(invitation.Handle)))
            .IsTypeOf<Result<ExternalMemberIssuanceReceipt, AeroError>.Failure>();
        await Assert.That((await replaySetup.Harness.Session.Query<ExternalMemberSession>().ToListAsync()).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Commit_failure_rolls_back_graph_and_leaves_invitation_unconsumed()
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Local.CreateInvitationAsync(LocalInvitation(setup)));
        setup.Listener.Reset();
        setup.Listener.Exception = new InvalidOperationException("database transport SECRET-DETAIL");

        var result = await setup.Local.ActivateInvitationAsync(Activation(invitation.Handle));
        setup.Listener.Exception = null;

        await Assert.That(((Result<ExternalMemberIssuanceReceipt, AeroError>.Failure)result).Error)
            .IsTypeOf<AeroError.Database>();
        await using var verification = await setup.Harness.OpenSessionAsync();
        await Assert.That(await verification.Query<ExternalMember>().ToListAsync()).IsEmpty();
        await Assert.That(await verification.Query<ExternalMemberLocalCredential>().ToListAsync()).IsEmpty();
        await Assert.That(await verification.Query<ExternalIdentityLink>().ToListAsync()).IsEmpty();
        await Assert.That((await verification.LoadAsync<ExternalMemberInvitation>(HandleId(invitation.Handle)))!.ConsumedAt).IsNull();
    }

    [Test]
    public async Task Concurrent_activation_of_one_invitation_commits_exactly_once()
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Local.CreateInvitationAsync(LocalInvitation(setup)));
        await using var firstSession = await setup.Harness.OpenSessionAsync();
        await using var secondSession = await setup.Harness.OpenSessionAsync();

        var results = await Task.WhenAll(
            CreateLocalService(firstSession, setup.Time).ActivateInvitationAsync(Activation(invitation.Handle)),
            CreateLocalService(secondSession, setup.Time).ActivateInvitationAsync(Activation(invitation.Handle)));

        await Assert.That(results.Count(value => value is Result<ExternalMemberIssuanceReceipt, AeroError>.Ok)).IsEqualTo(1);
        await using var verification = await setup.Harness.OpenSessionAsync();
        await Assert.That((await verification.Query<ExternalMember>().ToListAsync()).Count).IsEqualTo(1);
        await Assert.That((await verification.Query<ExternalMemberLocalCredential>().ToListAsync()).Count).IsEqualTo(1);
        await Assert.That((await verification.Query<ExternalMemberSession>().ToListAsync()).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Unknown_password_path_uses_one_sentinel_verification_without_regenerating_hash()
    {
        var hasher = new CountingPasswordHasher();
        await using var setup = await CreateSetupAsync(hasher);
        var hashesBeforeLogin = hasher.HashCalls;

        var first = await setup.Local.LoginAsync(new(TenantId, SiteId, "missing@example.com", Password, "/shop"));
        var second = await setup.Local.LoginAsync(new(TenantId, SiteId, "other@example.com", Password, "/shop"));

        await Assert.That(first).IsTypeOf<Result<ExternalMemberIssuanceReceipt, AeroError>.Failure>();
        await Assert.That(second).IsTypeOf<Result<ExternalMemberIssuanceReceipt, AeroError>.Failure>();
        await Assert.That(hasher.VerifyCalls).IsEqualTo(2);
        await Assert.That(hasher.HashCalls).IsEqualTo(hashesBeforeLogin);
    }

    [Test]
    public async Task Unknown_wrong_and_locked_logins_return_the_same_public_failure()
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Local.CreateInvitationAsync(LocalInvitation(setup)));
        _ = Ok(await setup.Local.ActivateInvitationAsync(Activation(invitation.Handle)));
        var wrong = await setup.Local.LoginAsync(new(TenantId, SiteId, Email, "wrong-password", "/shop"));
        var unknown = await setup.Local.LoginAsync(new(TenantId, SiteId, "unknown@example.com", Password, "/shop"));
        var credential = await setup.Harness.Session.Query<ExternalMemberLocalCredential>()
            .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.NormalizedEmail == "shopper@example.com");
        credential!.LockoutEndUtc = setup.Time.GetUtcNow().AddMinutes(15);
        setup.Harness.Session.Store(credential);
        await setup.Harness.Session.SaveChangesAsync();
        var locked = await setup.Local.LoginAsync(new(TenantId, SiteId, Email, Password, "/shop"));

        var wrongError = ((Result<ExternalMemberIssuanceReceipt, AeroError>.Failure)wrong).Error.ToString();
        var unknownError = ((Result<ExternalMemberIssuanceReceipt, AeroError>.Failure)unknown).Error.ToString();
        var lockedError = ((Result<ExternalMemberIssuanceReceipt, AeroError>.Failure)locked).Error.ToString();
        await Assert.That(unknownError).IsEqualTo(wrongError);
        await Assert.That(lockedError).IsEqualTo(wrongError);
    }

    [Test]
    public async Task Wrong_password_locks_on_fifth_failure_and_success_after_expiry_clears_lockout()
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Local.CreateInvitationAsync(LocalInvitation(setup)));
        _ = Ok(await setup.Local.ActivateInvitationAsync(Activation(invitation.Handle)));

        for (var attempt = 0; attempt < 5; attempt++)
            await setup.Local.LoginAsync(new(TenantId, SiteId, Email, "definitely-wrong", "/shop"));

        var credential = await setup.Harness.Session.Query<ExternalMemberLocalCredential>()
            .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.NormalizedEmail == "shopper@example.com");
        await Assert.That(credential!.FailedAccessCount).IsEqualTo(5);
        await Assert.That(credential.LockoutEndUtc).IsNotNull();
        await Assert.That(await setup.Local.LoginAsync(new(TenantId, SiteId, Email, Password, "/shop")))
            .IsTypeOf<Result<ExternalMemberIssuanceReceipt, AeroError>.Failure>();

        setup.Time.Advance(TimeSpan.FromMinutes(16));
        _ = Ok(await setup.Local.LoginAsync(new(TenantId, SiteId, Email, Password, "/shop")));
        await using var verification = await setup.Harness.OpenSessionAsync();
        var refreshedCredential = await verification.LoadAsync<ExternalMemberLocalCredential>(credential.Id);
        await Assert.That(refreshedCredential!.FailedAccessCount).IsEqualTo(0);
        await Assert.That(refreshedCredential.LockoutEndUtc).IsNull();
    }

    [Test]
    public async Task Successful_login_rehashes_an_identity_v2_password()
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Local.CreateInvitationAsync(LocalInvitation(setup)));
        _ = Ok(await setup.Local.ActivateInvitationAsync(Activation(invitation.Handle)));
        var credential = await setup.Harness.Session.Query<ExternalMemberLocalCredential>()
            .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.NormalizedEmail == "shopper@example.com");
        var legacyHasher = new PasswordHasher<ExternalMemberLocalCredential>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2
        }));
        credential!.PasswordHash = legacyHasher.HashPassword(credential, Password);
        setup.Harness.Session.Store(credential);
        await setup.Harness.Session.SaveChangesAsync();
        var legacyHash = credential.PasswordHash;

        _ = Ok(await setup.Local.LoginAsync(new(TenantId, SiteId, Email, Password, "/shop")));

        await using var verification = await setup.Harness.OpenSessionAsync();
        var refreshedCredential = await verification.LoadAsync<ExternalMemberLocalCredential>(credential.Id);
        await Assert.That(refreshedCredential!.PasswordHash).IsNotEqualTo(legacyHash);
    }

    [Test]
    public async Task Reset_consumes_once_bumps_versions_and_revokes_owned_sessions()
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Local.CreateInvitationAsync(LocalInvitation(setup)));
        var activation = Ok(await setup.Local.ActivateInvitationAsync(Activation(invitation.Handle)));
        var credential = await setup.Harness.Session.Query<ExternalMemberLocalCredential>()
            .FirstOrDefaultAsync(value => value.ExternalMemberId == activation.ExternalMemberId);
        var secret = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(new byte[32]);
        var reset = new ExternalMemberPasswordReset
        {
            Id = 8801, TenantId = TenantId, CredentialId = credential!.Id,
            TokenDigest = Digest(secret), CapturedCredentialSecurityVersion = credential.SecurityVersion,
            ExpiresAt = setup.Time.GetUtcNow().AddMinutes(30), IssuedByManagerUserId = 1
        };
        setup.Harness.Session.Store(reset);
        await setup.Harness.Session.SaveChangesAsync();

        var result = await setup.Local.ResetPasswordAsync(new(TenantId, SiteId, $"{reset.Id}.{secret}",
            "new correct horse battery staple", "/member/login"));
        var replay = await setup.Local.ResetPasswordAsync(new(TenantId, SiteId, $"{reset.Id}.{secret}",
            "another correct horse battery staple", "/member/login"));
        await using var verification = await setup.Harness.OpenSessionAsync();
        var persistedReset = await verification.LoadAsync<ExternalMemberPasswordReset>(reset.Id);
        var persistedCredential = await verification.LoadAsync<ExternalMemberLocalCredential>(credential.Id);
        var member = await verification.LoadAsync<ExternalMember>(activation.ExternalMemberId);
        var oldSession = await verification.LoadAsync<ExternalMemberSession>(activation.ExternalMemberSessionId);

        await Assert.That(result).IsTypeOf<Result<LocalExternalMemberPasswordResetReceipt, AeroError>.Ok>();
        await Assert.That(replay).IsTypeOf<Result<LocalExternalMemberPasswordResetReceipt, AeroError>.Failure>();
        await Assert.That(persistedReset!.ConsumedAt).IsNotNull();
        await Assert.That(persistedCredential!.SecurityVersion).IsEqualTo(2);
        await Assert.That(member!.SecurityVersion).IsEqualTo(2);
        await Assert.That(oldSession!.RevokedAt).IsNotNull();
    }

    [Test]
    public async Task Expired_reset_is_rejected_without_changing_security_versions()
    {
        await using var setup = await CreateSetupAsync();
        var invitation = Ok(await setup.Local.CreateInvitationAsync(LocalInvitation(setup)));
        var activation = Ok(await setup.Local.ActivateInvitationAsync(Activation(invitation.Handle)));
        var credential = await setup.Harness.Session.Query<ExternalMemberLocalCredential>()
            .FirstOrDefaultAsync(value => value.ExternalMemberId == activation.ExternalMemberId);
        var secret = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(new byte[32]);
        setup.Harness.Session.Store(new ExternalMemberPasswordReset
        {
            Id = 8802, TenantId = TenantId, CredentialId = credential!.Id,
            TokenDigest = Digest(secret), CapturedCredentialSecurityVersion = credential.SecurityVersion,
            ExpiresAt = setup.Time.GetUtcNow().AddMinutes(-1), IssuedByManagerUserId = 1
        });
        await setup.Harness.Session.SaveChangesAsync();

        var result = await setup.Local.ResetPasswordAsync(new(TenantId, SiteId, $"8802.{secret}",
            "new correct horse battery staple", "/member/login"));

        await Assert.That(result).IsTypeOf<Result<LocalExternalMemberPasswordResetReceipt, AeroError>.Failure>();
        await Assert.That(credential.SecurityVersion).IsEqualTo(1);
    }

    private static async Task<Setup> CreateSetupAsync(IPasswordHasher<ExternalMemberLocalCredential>? hasher = null)
    {
        var listener = new CommitFaultListener();
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero));
        var harness = new SableTestHarness().WithSchema<SitesModel>(SchemaMode.Flexible)
            .WithConfiguration(options => { new IdentityModule().Configure(options); options.Listeners.Add(listener); });
        await harness.InitializeAsync();
        harness.Session.Store(new SitesModel { Id = SiteId, TenantId = TenantId, Name = "Store", IsEnabled = true });
        harness.Session.Store(new ExternalMemberLocalAuthority { Id = AuthorityId, TenantId = TenantId, IsActive = true });
        harness.Session.Store(new ExternalOrganizationBinding
        {
            Id = RemoteBindingId, TenantId = TenantId, Provider = ExternalMemberProviders.WorkOs,
            Issuer = "https://api.workos.com", OrganizationId = "org_exact_A1",
            BindingKey = Key(ExternalMemberProviders.WorkOs, "https://api.workos.com", "org_exact_A1"),
            Authority = "https://api.workos.com", VaultId = 9001, VaultEnvironment = "test",
            CredentialPath = ExternalProviderSecretReference.CanonicalCredentialPath(TenantId, ExternalMemberProviders.WorkOs), IsActive = true
        });
        await harness.Session.SaveChangesAsync();
        listener.Reset();
        return new Setup(harness, CreateLocalService(harness.Session, time,
            hasher ?? new PasswordHasher<ExternalMemberLocalCredential>()), CreateRemoteService(harness.Session, time), time, listener);
    }

    private static LocalExternalMemberAuthenticationService CreateLocalService(IDocumentSession session, TimeProvider time) =>
        CreateLocalService(session, time, new PasswordHasher<ExternalMemberLocalCredential>());

    private static LocalExternalMemberAuthenticationService CreateLocalService(
        IDocumentSession session, TimeProvider time, IPasswordHasher<ExternalMemberLocalCredential> hasher) =>
        new(session, new CreateLocalExternalMemberInvitationRequestValidator(time),
            new ActivateLocalExternalMemberInvitationRequestValidator(),
            new LoginLocalExternalMemberRequestValidator(),
            new ResetLocalExternalMemberPasswordRequestValidator(),
            new IssueLocalExternalMemberPasswordResetRequestValidator(time),
            hasher, new LocalExternalMemberPasswordSentinel(hasher), time);

    private static ExternalMemberIssuanceService CreateRemoteService(IDocumentSession session, TimeProvider time) =>
        new(session, new CreateExternalMemberInvitationRequestValidator(time), new BeginExternalMemberSignInRequestValidator(),
            new CompleteExternalMemberSignInRequestValidator(time), time);

    private static CreateLocalExternalMemberInvitationRequest LocalInvitation(Setup setup) =>
        new(TenantId, SiteId, AuthorityId, Email, setup.Time.GetUtcNow().AddHours(1));

    private static ActivateLocalExternalMemberInvitationRequest Activation(string handle) =>
        new(TenantId, SiteId, handle, Email, Password, "Shopper", "/shop/account");

    private static T Ok<T>(Result<T, AeroError> result) => result is Result<T, AeroError>.Ok(var value)
        ? value : throw new InvalidOperationException($"Expected success but received {((Result<T, AeroError>.Failure)result).Error}.");

    private static long HandleId(string handle) => long.Parse(handle.AsSpan(0, handle.IndexOf('.')), CultureInfo.InvariantCulture);

    private static string Key(params string[] values)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (var value in values)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length); hash.AppendData(bytes);
        }
        return $"v1.{Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(hash.GetHashAndReset())}";
    }

    private static string Digest(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class CountingPasswordHasher : IPasswordHasher<ExternalMemberLocalCredential>
    {
        private readonly PasswordHasher<ExternalMemberLocalCredential> _inner = new();
        public int HashCalls { get; private set; }
        public int VerifyCalls { get; private set; }
        public string HashPassword(ExternalMemberLocalCredential user, string password)
        {
            HashCalls++;
            return _inner.HashPassword(user, password);
        }
        public PasswordVerificationResult VerifyHashedPassword(ExternalMemberLocalCredential user, string hashedPassword, string providedPassword)
        {
            VerifyCalls++;
            return _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }

    private sealed record Setup(SableTestHarness Harness, LocalExternalMemberAuthenticationService Local,
        ExternalMemberIssuanceService Remote, MutableTimeProvider Time, CommitFaultListener Listener) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Harness.DisposeAsync();
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    private sealed class CommitFaultListener : IDocumentSessionListener
    {
        public int SaveCalls { get; private set; }
        public Exception? Exception { get; set; }
        public Task BeforeSaveChangesAsync(IDocumentSession session, CancellationToken ct) { SaveCalls++; return Task.CompletedTask; }
        public Task BeforeCommitAsync(IDocumentSession session, CancellationToken ct) =>
            Exception is null ? Task.CompletedTask : Task.FromException(Exception);
        public void Reset() { SaveCalls = 0; Exception = null; }
    }
}
