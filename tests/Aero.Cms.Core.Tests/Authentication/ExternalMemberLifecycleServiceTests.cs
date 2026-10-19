using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Identity;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Models.Entities;
using AeroDB.Sable;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ExternalMemberLifecycleServiceTests
{
    private const long TenantId = 701;
    private const long SiteId = 702;
    private const long ManagerId = 703;
    private const long MemberId = 704;
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset TransitionAt = new(2026, 10, 13, 12, 1, 0, TimeSpan.Zero);

    [Test]
    public async Task Lifecycle_coordination_requires_one_participant_by_default()
    {
        var options = new ExternalMemberLifecycleOptions();

        await Assert.That(options.RequireParticipant).IsTrue();
    }

    [Test]
    public async Task Disable_enlists_the_exact_session_then_stages_member_session_and_existing_audit_fields_in_one_commit()
    {
        await using var setup = await CreateSetupAsync(new SuccessfulParticipant());
        var command = Command(isActive: false, expectedSecurityVersion: 3);

        var result = await setup.Service.TransitionAsync(command);

        await Assert.That(result).IsTypeOf<Result<ExternalMemberLifecycleResult, AeroError>.Ok>();
        var member = await setup.Harness.Session.LoadAsync<ExternalMember>(MemberId);
        var sessions = await setup.Harness.Session.Query<ExternalMemberSession>()
            .Where(value => value.ExternalMemberId == MemberId)
            .ToListAsync();

        await Assert.That(member!.IsActive).IsFalse();
        await Assert.That(member.SecurityVersion).IsEqualTo(4);
        await Assert.That(member.CreatedOn).IsEqualTo(CreatedAt);
        await Assert.That(member.CreatedBy).IsEqualTo("seed-import");
        await Assert.That(member.ModifiedOn).IsEqualTo(TransitionAt);
        await Assert.That(member.ModifiedBy).IsEqualTo(ManagerId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Assert.That(sessions.All(value => value.RevokedAt is not null)).IsTrue();
        await Assert.That(sessions.All(value => value.ModifiedOn == TransitionAt &&
            value.ModifiedBy == ManagerId.ToString(System.Globalization.CultureInfo.InvariantCulture))).IsTrue();
        await Assert.That(setup.Participant!.Session).IsSameReferenceAs(setup.Harness.Session);
        await Assert.That(setup.Participant.Calls).IsEqualTo(1);
        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(1);
    }

    [Test]
    public async Task Unchanged_activity_replay_returns_a_null_modification_time_without_rewriting_audit_fields()
    {
        await using var setup = await CreateSetupAsync(new SuccessfulParticipant());

        var result = await setup.Service.TransitionAsync(Command(isActive: true, expectedSecurityVersion: 3));

        await Assert.That(result).IsTypeOf<Result<ExternalMemberLifecycleResult, AeroError>.Ok>();
        var value = result is Result<ExternalMemberLifecycleResult, AeroError>.Ok(var lifecycle)
            ? lifecycle
            : throw new InvalidOperationException("Expected an idempotent lifecycle result.");
        var member = await setup.Harness.Session.LoadAsync<ExternalMember>(MemberId);
        await Assert.That(value.ModifiedOn).IsNull();
        await Assert.That(member!.ModifiedOn).IsNull();
        await Assert.That(member.ModifiedBy).IsNull();
        await Assert.That(member.SecurityVersion).IsEqualTo(3);
        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(0);
    }

    [Test]
    public async Task Participant_failure_aborts_before_any_member_session_or_audit_mutation()
    {
        var participant = new FailingParticipant();
        await using var setup = await CreateSetupAsync(participant);

        var result = await setup.Service.TransitionAsync(Command(isActive: false, expectedSecurityVersion: 3));

        await Assert.That(Error(result)).IsTypeOf<AeroError.Database>();
        await Assert.That(participant.Calls).IsEqualTo(1);
        var member = await setup.Harness.Session.LoadAsync<ExternalMember>(MemberId);
        await Assert.That(member!.IsActive).IsTrue();
        await Assert.That(member.SecurityVersion).IsEqualTo(3);
        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(0);
        await setup.Harness.Session.SaveChangesAsync();
        await using var readSession = await setup.Harness.OpenSessionAsync();
        var assignment = await readSession.LoadAsync<ExternalMemberSiteAssignment>(706);
        await Assert.That(assignment!.CustomerRole).IsEqualTo(ExternalMemberCustomerRole.GeneralUser);
    }

    [Test]
    public async Task Null_participant_acknowledgement_aborts_before_any_member_session_or_audit_mutation()
    {
        var participant = new NullReceiptParticipant();
        await using var setup = await CreateSetupAsync(participant);

        var result = await setup.Service.TransitionAsync(Command(isActive: false, expectedSecurityVersion: 3));

        await Assert.That(Error(result)).IsTypeOf<AeroError.Database>();
        await Assert.That(participant.Calls).IsEqualTo(1);
        await Assert.That((await setup.Harness.Session.LoadAsync<ExternalMember>(MemberId))!.IsActive).IsTrue();
        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(0);
    }

    [Test]
    public async Task Malformed_participant_acknowledgement_aborts_before_any_member_session_or_audit_mutation()
    {
        var participant = new MalformedReceiptParticipant();
        await using var setup = await CreateSetupAsync(participant);

        var result = await setup.Service.TransitionAsync(Command(isActive: false, expectedSecurityVersion: 3));

        await Assert.That(Error(result)).IsTypeOf<AeroError.Database>();
        await Assert.That(participant.Calls).IsEqualTo(1);
        await Assert.That((await setup.Harness.Session.LoadAsync<ExternalMember>(MemberId))!.IsActive).IsTrue();
        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(0);
    }

    [Test]
    public async Task Required_zero_or_multiple_participants_are_unavailable_before_mutation()
    {
        await using var zero = await CreateSetupAsync();
        await using var multiple = await CreateSetupAsync(new SuccessfulParticipant(), new SuccessfulParticipant());

        var zeroResult = await zero.Service.TransitionAsync(Command(isActive: false, expectedSecurityVersion: 3));
        var multipleResult = await multiple.Service.TransitionAsync(Command(isActive: false, expectedSecurityVersion: 3));

        await Assert.That(zeroResult).IsTypeOf<Result<ExternalMemberLifecycleResult, AeroError>.Failure>();
        await Assert.That(multipleResult).IsTypeOf<Result<ExternalMemberLifecycleResult, AeroError>.Failure>();
        await Assert.That((await zero.Harness.Session.LoadAsync<ExternalMember>(MemberId))!.IsActive).IsTrue();
        await Assert.That((await multiple.Harness.Session.LoadAsync<ExternalMember>(MemberId))!.IsActive).IsTrue();
    }

    [Test]
    public async Task Multiple_participants_are_unavailable_even_when_coordination_is_not_required()
    {
        await using var setup = await CreateSetupAsync(
            requireParticipant: false,
            new SuccessfulParticipant(),
            new SuccessfulParticipant());

        var result = await setup.Service.TransitionAsync(Command(isActive: false, expectedSecurityVersion: 3));

        await Assert.That(result).IsTypeOf<Result<ExternalMemberLifecycleResult, AeroError>.Failure>();
        await Assert.That((await setup.Harness.Session.LoadAsync<ExternalMember>(MemberId))!.IsActive).IsTrue();
    }

    [Test]
    public async Task Standalone_host_allows_zero_participants_when_coordination_is_not_required()
    {
        await using var setup = await CreateSetupAsync(requireParticipant: false);

        var result = await setup.Service.TransitionAsync(Command(isActive: false, expectedSecurityVersion: 3));

        await Assert.That(result).IsTypeOf<Result<ExternalMemberLifecycleResult, AeroError>.Ok>();
        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(1);
    }

    [Test]
    public async Task Stale_security_version_conflicts_without_granting_or_restoring_assignments()
    {
        await using var setup = await CreateSetupAsync(new SuccessfulParticipant());

        var result = await setup.Service.TransitionAsync(Command(isActive: false, expectedSecurityVersion: 2));

        await Assert.That(Error(result)).IsTypeOf<AeroError.Conflict>();
        var assignment = await setup.Harness.Session.Query<ExternalMemberSiteAssignment>()
            .FirstOrDefaultAsync(value => value.ExternalMemberId == MemberId && value.SiteId == SiteId);
        await Assert.That(assignment).IsNotNull();
        await Assert.That(assignment!.Id).IsEqualTo(706);
        await Assert.That(assignment.IsActive).IsTrue();
        await Assert.That(assignment.CustomerRole).IsEqualTo(ExternalMemberCustomerRole.GeneralUser);
    }

    [Test]
    public async Task Missing_or_foreign_target_assignment_is_rejected_before_participant_enlistment()
    {
        await using var setup = await CreateSetupAsync(new SuccessfulParticipant());

        var missing = await setup.Service.TransitionAsync(
            Command(isActive: false, expectedSecurityVersion: 3) with { SiteId = SiteId + 1 });
        var foreign = await setup.Service.TransitionAsync(
            Command(isActive: false, expectedSecurityVersion: 3) with { TenantId = TenantId + 1 });

        await Assert.That(Error(missing)).IsTypeOf<AeroError.NotFound>();
        await Assert.That(Error(foreign)).IsTypeOf<AeroError.NotFound>();
        await Assert.That(setup.Participant!.Calls).IsEqualTo(0);
        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(0);
    }

    [Test]
    public async Task Reactivation_does_not_restore_roles_or_unrevoke_existing_sessions()
    {
        await using var setup = await CreateSetupAsync(new SuccessfulParticipant());
        await MakeMemberInactiveAsync(setup);

        var result = await setup.Service.TransitionAsync(Command(isActive: true, expectedSecurityVersion: 3));

        await Assert.That(result).IsTypeOf<Result<ExternalMemberLifecycleResult, AeroError>.Ok>();
        var assignments = await setup.Harness.Session.Query<ExternalMemberSiteAssignment>()
            .Where(value => value.ExternalMemberId == MemberId)
            .ToListAsync();
        var sessions = await setup.Harness.Session.Query<ExternalMemberSession>()
            .Where(value => value.ExternalMemberId == MemberId)
            .ToListAsync();
        await Assert.That(assignments.Count).IsEqualTo(1);
        await Assert.That(assignments[0].CustomerRole).IsEqualTo(ExternalMemberCustomerRole.GeneralUser);
        await Assert.That(sessions.All(value => value.RevokedAt is not null)).IsTrue();
    }

    [Test]
    public async Task Refreshed_expected_version_same_activity_is_state_setting_idempotence_and_preserves_audit_timestamp()
    {
        await using var setup = await CreateSetupAsync(new SuccessfulParticipant());
        var command = Command(isActive: false, expectedSecurityVersion: 3) with { CorrelationId = "opaque-lifecycle-1" };

        var first = await setup.Service.TransitionAsync(command);
        setup.Time.Advance(TimeSpan.FromMinutes(5));
        var second = await setup.Service.TransitionAsync(command with { ExpectedSecurityVersion = 4 });

        await Assert.That(first).IsTypeOf<Result<ExternalMemberLifecycleResult, AeroError>.Ok>();
        await Assert.That(second).IsTypeOf<Result<ExternalMemberLifecycleResult, AeroError>.Ok>();
        var replay = second is Result<ExternalMemberLifecycleResult, AeroError>.Ok(var value)
            ? value
            : throw new InvalidOperationException("Expected an idempotent lifecycle result.");
        var member = await setup.Harness.Session.LoadAsync<ExternalMember>(MemberId);
        await Assert.That(member!.SecurityVersion).IsEqualTo(4);
        await Assert.That(member.ModifiedOn).IsEqualTo(TransitionAt);
        await Assert.That(replay.ModifiedOn).IsEqualTo(TransitionAt);
        await Assert.That(setup.Participant!.Calls).IsEqualTo(1);
        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(1);
    }

    [Test]
    public async Task Stale_expected_version_is_a_conflict_even_when_the_correlation_and_activity_match()
    {
        await using var setup = await CreateSetupAsync(new SuccessfulParticipant());
        var command = Command(isActive: false, expectedSecurityVersion: 3) with { CorrelationId = "opaque-lifecycle-1" };

        var first = await setup.Service.TransitionAsync(command);
        var staleReplay = await setup.Service.TransitionAsync(command);

        await Assert.That(first).IsTypeOf<Result<ExternalMemberLifecycleResult, AeroError>.Ok>();
        await Assert.That(Error(staleReplay)).IsTypeOf<AeroError.Conflict>();
        await Assert.That(setup.Participant!.Calls).IsEqualTo(1);
        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(1);
    }

    [Test]
    public async Task Security_version_overflow_is_rejected_before_participant_or_save()
    {
        await using var setup = await CreateSetupAsync(new SuccessfulParticipant());
        var member = (await setup.Harness.Session.LoadAsync<ExternalMember>(MemberId))!;
        member.SecurityVersion = long.MaxValue;
        setup.Harness.Session.Store(member);
        await setup.Harness.Session.SaveChangesAsync();
        setup.Listener.Reset();

        var result = await setup.Service.TransitionAsync(Command(isActive: false, expectedSecurityVersion: long.MaxValue));

        await Assert.That(result).IsTypeOf<Result<ExternalMemberLifecycleResult, AeroError>.Failure>();
        await Assert.That(setup.Participant!.Calls).IsEqualTo(0);
        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(0);
    }

    [Test]
    public async Task Participant_exception_is_redacted_after_one_enlistment_without_a_save()
    {
        var participant = new ThrowingParticipant();
        await using var setup = await CreateSetupAsync(participant);

        var result = await setup.Service.TransitionAsync(Command(isActive: false, expectedSecurityVersion: 3));

        await Assert.That(Error(result)).IsTypeOf<AeroError.Database>();
        await Assert.That(Error(result).ToString()).DoesNotContain("private participant detail");
        await Assert.That(participant.Calls).IsEqualTo(1);
        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(0);
    }

    [Test]
    public async Task Optimistic_concurrency_conflict_is_fixed_and_does_not_retry()
    {
        await using var setup = await CreateSetupAsync(new SuccessfulParticipant());
        setup.Listener.Exception = new ConcurrencyException(typeof(ExternalMember), MemberId, 3, 4);

        var result = await setup.Service.TransitionAsync(Command(isActive: false, expectedSecurityVersion: 3));

        await Assert.That(Error(result)).IsTypeOf<AeroError.Conflict>();
        await Assert.That(setup.Participant!.Calls).IsEqualTo(1);
        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(1);
    }

    [Test]
    public async Task Commit_outcome_unknown_is_propagated_without_an_automatic_retry()
    {
        await using var setup = await CreateSetupAsync(new SuccessfulParticipant());
        setup.Listener.Exception = new CommitOutcomeUnknownException(new InvalidOperationException("private transport detail"));

        await Assert.That(async () => await setup.Service.TransitionAsync(
            Command(isActive: false, expectedSecurityVersion: 3)))
            .Throws<CommitOutcomeUnknownException>();
        await Assert.That(setup.Participant!.Calls).IsEqualTo(1);
        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(1);
    }

    [Test]
    public async Task Post_commit_callback_failure_is_propagated_without_an_automatic_retry()
    {
        await using var setup = await CreateSetupAsync(new SuccessfulParticipant());
        setup.Listener.PostCommitException = new InvalidOperationException("private callback detail");

        await Assert.That(async () => await setup.Service.TransitionAsync(
            Command(isActive: false, expectedSecurityVersion: 3)))
            .Throws<PostCommitCallbackException>();
        await Assert.That(setup.Participant!.Calls).IsEqualTo(1);
        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(1);
        await using var readSession = await setup.Harness.OpenSessionAsync();
        var member = await readSession.LoadAsync<ExternalMember>(MemberId);
        await Assert.That(member!.IsActive).IsFalse();
        await Assert.That(member.SecurityVersion).IsEqualTo(4);
    }

    [Test]
    public async Task Precommit_cancellation_is_preserved_without_writes_or_error_detail_disclosure()
    {
        await using var setup = await CreateSetupAsync(new SuccessfulParticipant());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            await setup.Service.TransitionAsync(Command(isActive: false, expectedSecurityVersion: 3), cancellation.Token);
            throw new InvalidOperationException("Expected cancellation.");
        }
        catch (OperationCanceledException exception)
        {
            await Assert.That(exception.CancellationToken).IsEqualTo(cancellation.Token);
        }
        var member = await setup.Harness.Session.LoadAsync<ExternalMember>(MemberId);
        await Assert.That(member!.IsActive).IsTrue();
        await Assert.That(setup.Listener.SaveCalls).IsEqualTo(0);
    }

    private static ExternalMemberLifecycleCommand Command(bool isActive, long expectedSecurityVersion) => new(
        TenantId, SiteId, ManagerId, MemberId, isActive, expectedSecurityVersion, null);

    private static AeroError Error(Result<ExternalMemberLifecycleResult, AeroError> result) => result is
        Result<ExternalMemberLifecycleResult, AeroError>.Failure(var error)
            ? error
            : throw new InvalidOperationException("Expected lifecycle failure.");

    private static async Task<Setup> CreateSetupAsync(params IExternalMemberLifecycleParticipant[] participants) =>
        await CreateSetupAsync(requireParticipant: true, participants);

    private static async Task<Setup> CreateSetupAsync(
        bool requireParticipant,
        params IExternalMemberLifecycleParticipant[] participants)
    {
        var listener = new SaveCountingListener();
        var harness = new SableTestHarness()
            .WithSchema<SitesModel>(SchemaMode.Flexible)
            .WithConfiguration(options =>
            {
                new IdentityModule().Configure(options);
                options.Listeners.Add(listener);
            });
        await harness.InitializeAsync();
        harness.Session.Store(new SitesModel { Id = SiteId, TenantId = TenantId, Name = "Lifecycle test", IsEnabled = true });
        harness.Session.Store(new ExternalMember
        {
            Id = MemberId,
            IsActive = true,
            SecurityVersion = 3,
            CreatedOn = CreatedAt,
            CreatedBy = "seed-import"
        });
        harness.Session.Store(new ExternalMemberSession
        {
            Id = 705,
            ExternalMemberId = MemberId,
            TenantId = TenantId,
            SiteId = SiteId,
            AuthenticationProvider = "workos",
            SecurityVersion = 3,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        });
        harness.Session.Store(new ExternalMemberSiteAssignment
        {
            Id = 706,
            ExternalMemberId = MemberId,
            TenantId = TenantId,
            SiteId = SiteId,
            IsActive = true,
            CustomerRole = ExternalMemberCustomerRole.GeneralUser,
            CreatedOn = CreatedAt,
            CreatedBy = "seed-import"
        });
        await harness.Session.SaveChangesAsync();
        listener.Reset();

        var participant = participants.OfType<SuccessfulParticipant>().FirstOrDefault();
        var time = new AdvancingTimeProvider(TransitionAt);
        var service = new ExternalMemberLifecycleService(
            harness.Session,
            new ExternalMemberLifecycleCommandValidator(),
            participants,
            Options.Create(new ExternalMemberLifecycleOptions { RequireParticipant = requireParticipant }),
            time);
        return new Setup(harness, service, participant, listener, time);
    }

    private static async Task MakeMemberInactiveAsync(Setup setup)
    {
        var member = (await setup.Harness.Session.LoadAsync<ExternalMember>(MemberId))!;
        member.IsActive = false;
        setup.Harness.Session.Store(member);
        var sessions = await setup.Harness.Session.Query<ExternalMemberSession>()
            .Where(value => value.ExternalMemberId == MemberId)
            .ToListAsync();
        foreach (var session in sessions)
        {
            session.RevokedAt = TransitionAt;
            setup.Harness.Session.Store(session);
        }
        await setup.Harness.Session.SaveChangesAsync();
        setup.Listener.Reset();
    }

    private sealed record Setup(
        SableTestHarness Harness,
        ExternalMemberLifecycleService Service,
        SuccessfulParticipant? Participant,
        SaveCountingListener Listener,
        AdvancingTimeProvider Time) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Harness.DisposeAsync();
    }

    private sealed class SuccessfulParticipant : IExternalMemberLifecycleParticipant
    {
        public IDocumentSession? Session { get; private set; }
        public int Calls { get; private set; }

        public Task<Result<ExternalMemberLifecycleParticipantReceipt, AeroError>> EnlistAsync(
            IDocumentSession session,
            ExternalMemberLifecycleTransition transition,
            CancellationToken cancellationToken = default)
        {
            Session = session;
            Calls++;
            return Task.FromResult(Prelude.Ok<ExternalMemberLifecycleParticipantReceipt, AeroError>(
                new("prepared")));
        }
    }

    private sealed class FailingParticipant : IExternalMemberLifecycleParticipant
    {
        public int Calls { get; private set; }

        public async Task<Result<ExternalMemberLifecycleParticipantReceipt, AeroError>> EnlistAsync(
            IDocumentSession session,
            ExternalMemberLifecycleTransition transition,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            var assignment = (await session.LoadAsync<ExternalMemberSiteAssignment>(706, cancellationToken))!;
            assignment.CustomerRole = ExternalMemberCustomerRole.PowerUser;
            session.Store(assignment);
            return Prelude.Fail<ExternalMemberLifecycleParticipantReceipt, AeroError>(
                AeroError.DatabaseError("participant unavailable"));
        }
    }

    private sealed class ThrowingParticipant : IExternalMemberLifecycleParticipant
    {
        public int Calls { get; private set; }

        public Task<Result<ExternalMemberLifecycleParticipantReceipt, AeroError>> EnlistAsync(
            IDocumentSession session,
            ExternalMemberLifecycleTransition transition,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("private participant detail");
        }
    }

    private sealed class NullReceiptParticipant : IExternalMemberLifecycleParticipant
    {
        public int Calls { get; private set; }

        public Task<Result<ExternalMemberLifecycleParticipantReceipt, AeroError>> EnlistAsync(
            IDocumentSession session,
            ExternalMemberLifecycleTransition transition,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Prelude.Ok<ExternalMemberLifecycleParticipantReceipt, AeroError>(null!));
        }
    }

    private sealed class MalformedReceiptParticipant : IExternalMemberLifecycleParticipant
    {
        public int Calls { get; private set; }

        public Task<Result<ExternalMemberLifecycleParticipantReceipt, AeroError>> EnlistAsync(
            IDocumentSession session,
            ExternalMemberLifecycleTransition transition,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Prelude.Ok<ExternalMemberLifecycleParticipantReceipt, AeroError>(new("")));
        }
    }

    private sealed class AdvancingTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan duration) => utcNow = utcNow.Add(duration);
    }

    private sealed class SaveCountingListener : IDocumentSessionListener
    {
        public int SaveCalls { get; private set; }
        public Exception? Exception { get; set; }
        public Exception? PostCommitException { get; set; }

        public Task BeforeSaveChangesAsync(IDocumentSession session, CancellationToken cancellationToken)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }

        public Task BeforeCommitAsync(IDocumentSession session, CancellationToken cancellationToken) =>
            Exception is null ? Task.CompletedTask : Task.FromException(Exception);

        public Task AfterCommitAsync(IDocumentSession session, IChangeSet changes, CancellationToken cancellationToken) =>
            PostCommitException is null ? Task.CompletedTask : Task.FromException(PostCommitException);

        public void Reset()
        {
            SaveCalls = 0;
            Exception = null;
            PostCommitException = null;
        }
    }
}
