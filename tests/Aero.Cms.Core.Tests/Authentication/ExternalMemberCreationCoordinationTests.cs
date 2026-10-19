using Aero.Cms.Modules.Identity;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ExternalMemberCreationCoordinationTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Required_local_creation_enlists_exactly_one_participant_on_the_supplied_session()
    {
        var session = Substitute.For<IDocumentSession>();
        var participant = new SuccessfulParticipant();
        var coordinator = Create(required: true, participant);
        var transition = NewTransition(ExternalMemberCreationKind.LocalCredential);

        var result = await coordinator.EnlistAsync(session, transition);

        await Assert.That(result).IsTypeOf<Result<ExternalMemberCreationParticipantReceipt, AeroError>.Ok>();
        await Assert.That(participant.Session).IsSameReferenceAs(session);
        await Assert.That(participant.Transition).IsEqualTo(transition);
        await Assert.That(participant.Calls).IsEqualTo(1);
    }

    [Test]
    public async Task Required_federated_creation_enlists_the_immutable_federated_transition()
    {
        var session = Substitute.For<IDocumentSession>();
        var participant = new SuccessfulParticipant();
        var coordinator = Create(required: true, participant);
        var transition = NewTransition(ExternalMemberCreationKind.FederatedIdentity);

        var result = await coordinator.EnlistAsync(session, transition);

        await Assert.That(result).IsTypeOf<Result<ExternalMemberCreationParticipantReceipt, AeroError>.Ok>();
        await Assert.That(participant.Transition).IsEqualTo(transition);
        await Assert.That(participant.Transition!.Kind).IsEqualTo(ExternalMemberCreationKind.FederatedIdentity);
    }

    [Test]
    public async Task Standalone_host_allows_new_member_creation_without_a_participant()
    {
        var coordinator = Create(required: false);

        var result = await coordinator.EnlistAsync(Substitute.For<IDocumentSession>(), NewTransition(ExternalMemberCreationKind.LocalCredential));

        await Assert.That(result).IsTypeOf<Result<ExternalMemberCreationParticipantReceipt, AeroError>.Ok>();
    }

    [Test]
    public async Task Required_missing_participant_is_denied_before_any_participant_can_run()
    {
        var result = await Create(required: true).EnlistAsync(
            Substitute.For<IDocumentSession>(), NewTransition(ExternalMemberCreationKind.LocalCredential));

        await Assert.That(result).IsTypeOf<Result<ExternalMemberCreationParticipantReceipt, AeroError>.Failure>();
    }

    [Test]
    public async Task Multiple_participants_are_denied_even_when_coordination_is_optional()
    {
        var first = new SuccessfulParticipant();
        var second = new SuccessfulParticipant();
        var result = await Create(required: false, first, second).EnlistAsync(
            Substitute.For<IDocumentSession>(), NewTransition(ExternalMemberCreationKind.FederatedIdentity));

        await Assert.That(result).IsTypeOf<Result<ExternalMemberCreationParticipantReceipt, AeroError>.Failure>();
        await Assert.That(first.Calls).IsEqualTo(0);
        await Assert.That(second.Calls).IsEqualTo(0);
    }

    [Test]
    public async Task Mismatched_participant_receipt_is_denied_without_a_second_participant()
    {
        var participant = new SuccessfulParticipant(transition => new(
            transition with { ExternalMemberId = transition.ExternalMemberId + 1 }, "prepared"));
        var result = await Create(required: true, participant).EnlistAsync(
            Substitute.For<IDocumentSession>(), NewTransition(ExternalMemberCreationKind.LocalCredential));

        await Assert.That(result).IsTypeOf<Result<ExternalMemberCreationParticipantReceipt, AeroError>.Failure>();
        await Assert.That(participant.Calls).IsEqualTo(1);
    }

    [Test]
    [Arguments("tenant")]
    [Arguments("site")]
    [Arguments("member")]
    [Arguments("security-version")]
    [Arguments("kind")]
    [Arguments("occurred-at")]
    [Arguments("time-offset")]
    public async Task Participant_receipt_must_acknowledge_every_immutable_transition_field(string field)
    {
        var participant = new SuccessfulParticipant(transition => new(Mutate(transition, field), "prepared"));

        var result = await Create(required: true, participant).EnlistAsync(
            Substitute.For<IDocumentSession>(), NewTransition(ExternalMemberCreationKind.LocalCredential));

        await Assert.That(result).IsTypeOf<Result<ExternalMemberCreationParticipantReceipt, AeroError>.Failure>();
        await Assert.That(participant.Calls).IsEqualTo(1);
    }

    [Test]
    public async Task Null_or_malformed_participant_receipts_are_denied()
    {
        var nullReceipt = new SuccessfulParticipant(_ => null!);
        var malformedReceipt = new SuccessfulParticipant(transition => new(transition, "\t"));

        var nullResult = await Create(required: true, nullReceipt).EnlistAsync(
            Substitute.For<IDocumentSession>(), NewTransition(ExternalMemberCreationKind.LocalCredential));
        var malformedResult = await Create(required: true, malformedReceipt).EnlistAsync(
            Substitute.For<IDocumentSession>(), NewTransition(ExternalMemberCreationKind.LocalCredential));

        await Assert.That(nullResult).IsTypeOf<Result<ExternalMemberCreationParticipantReceipt, AeroError>.Failure>();
        await Assert.That(malformedResult).IsTypeOf<Result<ExternalMemberCreationParticipantReceipt, AeroError>.Failure>();
    }

    [Test]
    public async Task Null_session_is_denied_before_participant_enlistment()
    {
        var participant = new SuccessfulParticipant();
        var result = await Create(required: true, participant).EnlistAsync(
            null!, NewTransition(ExternalMemberCreationKind.LocalCredential));

        await Assert.That(result).IsTypeOf<Result<ExternalMemberCreationParticipantReceipt, AeroError>.Failure>();
        await Assert.That(participant.Calls).IsEqualTo(0);
    }

    [Test]
    [Arguments(0L)]
    [Arguments(-1L)]
    [Arguments(2L)]
    public async Task Creation_security_version_must_be_exactly_one(long securityVersion)
    {
        var participant = new SuccessfulParticipant();
        var result = await Create(required: true, participant).EnlistAsync(
            Substitute.For<IDocumentSession>(), NewTransition(ExternalMemberCreationKind.LocalCredential) with { SecurityVersion = securityVersion });

        await Assert.That(result).IsTypeOf<Result<ExternalMemberCreationParticipantReceipt, AeroError>.Failure>();
        await Assert.That(participant.Calls).IsEqualTo(0);
    }

    [Test]
    [Arguments("tenant")]
    [Arguments("site")]
    [Arguments("member")]
    [Arguments("kind")]
    [Arguments("default-time")]
    [Arguments("non-utc-time")]
    public async Task Malformed_creation_transition_is_denied_before_participant_enlistment(string field)
    {
        var participant = new SuccessfulParticipant();
        var result = await Create(required: true, participant).EnlistAsync(
            Substitute.For<IDocumentSession>(), MalformedTransition(field));

        await Assert.That(result).IsTypeOf<Result<ExternalMemberCreationParticipantReceipt, AeroError>.Failure>();
        await Assert.That(participant.Calls).IsEqualTo(0);
    }

    [Test]
    public async Task Throwing_options_are_redacted_as_a_denial()
    {
        var result = new ExternalMemberCreationCoordinator(
            [new SuccessfulParticipant()], new ThrowingOptions()).EnlistAsync(
                Substitute.For<IDocumentSession>(), NewTransition(ExternalMemberCreationKind.LocalCredential));

        var denied = await result;
        await Assert.That(denied).IsTypeOf<Result<ExternalMemberCreationParticipantReceipt, AeroError>.Failure>();
        var failure = (Result<ExternalMemberCreationParticipantReceipt, AeroError>.Failure)denied;
        await Assert.That(failure.Error.ToString()).DoesNotContain("private options detail");
    }

    [Test]
    public async Task Throwing_participant_enumeration_is_redacted_as_a_denial()
    {
        var result = new ExternalMemberCreationCoordinator(
            new ThrowingParticipantEnumerable(), Options.Create(new ExternalMemberCreationCoordinationOptions { RequireParticipant = true })).EnlistAsync(
                Substitute.For<IDocumentSession>(), NewTransition(ExternalMemberCreationKind.LocalCredential));

        var denied = await result;
        await Assert.That(denied).IsTypeOf<Result<ExternalMemberCreationParticipantReceipt, AeroError>.Failure>();
        var failure = (Result<ExternalMemberCreationParticipantReceipt, AeroError>.Failure)denied;
        await Assert.That(failure.Error.ToString()).DoesNotContain("private enumerable detail");
    }

    [Test]
    public async Task Identity_module_registers_optional_scoped_member_creation_coordination()
    {
        var services = new ServiceCollection();

        new IdentityModule().ConfigureServices(services);

        var coordinator = services.SingleOrDefault(descriptor =>
            descriptor.ServiceType == typeof(ExternalMemberCreationCoordinator));
        await Assert.That(coordinator).IsNotNull();
        await Assert.That(coordinator!.Lifetime).IsEqualTo(ServiceLifetime.Scoped);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ExternalMemberCreationCoordinationOptions>>();
        await Assert.That(options.Value.RequireParticipant).IsFalse();
    }

    [Test]
    public async Task Failed_participant_denies_creation_without_disclosing_its_detail()
    {
        var participant = new FailingParticipant();
        var result = await Create(required: true, participant).EnlistAsync(
            Substitute.For<IDocumentSession>(), NewTransition(ExternalMemberCreationKind.LocalCredential));

        await Assert.That(result).IsTypeOf<Result<ExternalMemberCreationParticipantReceipt, AeroError>.Failure>();
        var failure = (Result<ExternalMemberCreationParticipantReceipt, AeroError>.Failure)result;
        await Assert.That(failure.Error.ToString()).DoesNotContain("private participant detail");
        await Assert.That(participant.Calls).IsEqualTo(1);
    }

    [Test]
    public async Task Caller_cancellation_is_preserved_before_participant_enlistment()
    {
        var participant = new SuccessfulParticipant();
        var coordinator = Create(required: true, participant);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.That(async () => await coordinator.EnlistAsync(
            Substitute.For<IDocumentSession>(), NewTransition(ExternalMemberCreationKind.LocalCredential), cancellation.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(participant.Calls).IsEqualTo(0);
    }

    [Test]
    public async Task Participant_cancellation_with_the_caller_token_is_preserved()
    {
        using var cancellation = new CancellationTokenSource();
        var participant = new CancellingParticipant(cancellation);

        await Assert.That(async () => await Create(required: true, participant).EnlistAsync(
            Substitute.For<IDocumentSession>(), NewTransition(ExternalMemberCreationKind.LocalCredential), cancellation.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(participant.Calls).IsEqualTo(1);
    }

    [Test]
    public async Task Participant_exceptions_are_redacted_as_a_denial()
    {
        var participant = new ThrowingParticipant();
        var result = await Create(required: true, participant).EnlistAsync(
            Substitute.For<IDocumentSession>(), NewTransition(ExternalMemberCreationKind.LocalCredential));

        await Assert.That(result).IsTypeOf<Result<ExternalMemberCreationParticipantReceipt, AeroError>.Failure>();
        var failure = (Result<ExternalMemberCreationParticipantReceipt, AeroError>.Failure)result;
        await Assert.That(failure.Error.ToString()).DoesNotContain("private exception detail");
        await Assert.That(participant.Calls).IsEqualTo(1);
    }

    private static ExternalMemberCreationCoordinator Create(
        bool required,
        params IExternalMemberCreationParticipant[] participants) =>
        new(participants, Options.Create(new ExternalMemberCreationCoordinationOptions { RequireParticipant = required }));

    private static ExternalMemberCreatedTransition NewTransition(ExternalMemberCreationKind kind) =>
        new(101, 102, 103, 1, kind, OccurredAt);

    private static ExternalMemberCreatedTransition Mutate(ExternalMemberCreatedTransition transition, string field) => field switch
    {
        "tenant" => transition with { TenantId = transition.TenantId + 1 },
        "site" => transition with { SiteId = transition.SiteId + 1 },
        "member" => transition with { ExternalMemberId = transition.ExternalMemberId + 1 },
        "security-version" => transition with { SecurityVersion = transition.SecurityVersion + 1 },
        "kind" => transition with { Kind = ExternalMemberCreationKind.FederatedIdentity },
        "occurred-at" => transition with { OccurredAtUtc = transition.OccurredAtUtc.AddSeconds(1) },
        "time-offset" => transition with { OccurredAtUtc = transition.OccurredAtUtc.ToOffset(TimeSpan.FromHours(-5)) },
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };

    private static ExternalMemberCreatedTransition MalformedTransition(string field) => field switch
    {
        "tenant" => NewTransition(ExternalMemberCreationKind.LocalCredential) with { TenantId = 0 },
        "site" => NewTransition(ExternalMemberCreationKind.LocalCredential) with { SiteId = 0 },
        "member" => NewTransition(ExternalMemberCreationKind.LocalCredential) with { ExternalMemberId = 0 },
        "kind" => NewTransition((ExternalMemberCreationKind)99),
        "default-time" => NewTransition(ExternalMemberCreationKind.LocalCredential) with { OccurredAtUtc = default },
        "non-utc-time" => NewTransition(ExternalMemberCreationKind.LocalCredential) with
        {
            OccurredAtUtc = new DateTimeOffset(2026, 10, 14, 7, 0, 0, TimeSpan.FromHours(-5))
        },
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };

    private sealed class SuccessfulParticipant(Func<ExternalMemberCreatedTransition, ExternalMemberCreationParticipantReceipt>? receiptFactory = null)
        : IExternalMemberCreationParticipant
    {
        public IDocumentSession? Session { get; private set; }
        public ExternalMemberCreatedTransition? Transition { get; private set; }
        public int Calls { get; private set; }

        public Task<Result<ExternalMemberCreationParticipantReceipt, AeroError>> EnlistAsync(
            IDocumentSession session,
            ExternalMemberCreatedTransition transition,
            CancellationToken cancellationToken = default)
        {
            Session = session;
            Transition = transition;
            Calls++;
            return Task.FromResult(Prelude.Ok<ExternalMemberCreationParticipantReceipt, AeroError>(
                receiptFactory is null ? new(transition, "prepared") : receiptFactory(transition)));
        }
    }

    private sealed class FailingParticipant : IExternalMemberCreationParticipant
    {
        public int Calls { get; private set; }

        public Task<Result<ExternalMemberCreationParticipantReceipt, AeroError>> EnlistAsync(
            IDocumentSession session,
            ExternalMemberCreatedTransition transition,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Prelude.Fail<ExternalMemberCreationParticipantReceipt, AeroError>(
                AeroError.DatabaseError("private participant detail")));
        }
    }

    private sealed class CancellingParticipant(CancellationTokenSource cancellation) : IExternalMemberCreationParticipant
    {
        public int Calls { get; private set; }

        public Task<Result<ExternalMemberCreationParticipantReceipt, AeroError>> EnlistAsync(
            IDocumentSession session,
            ExternalMemberCreatedTransition transition,
            CancellationToken callerCancellationToken = default)
        {
            Calls++;
            cancellation.Cancel();
            return Task.FromCanceled<Result<ExternalMemberCreationParticipantReceipt, AeroError>>(cancellation.Token);
        }
    }

    private sealed class ThrowingParticipant : IExternalMemberCreationParticipant
    {
        public int Calls { get; private set; }

        public Task<Result<ExternalMemberCreationParticipantReceipt, AeroError>> EnlistAsync(
            IDocumentSession session,
            ExternalMemberCreatedTransition transition,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("private exception detail");
        }
    }

    private sealed class ThrowingOptions : IOptions<ExternalMemberCreationCoordinationOptions>
    {
        public ExternalMemberCreationCoordinationOptions Value =>
            throw new InvalidOperationException("private options detail");
    }

    private sealed class ThrowingParticipantEnumerable : IEnumerable<IExternalMemberCreationParticipant>
    {
        public IEnumerator<IExternalMemberCreationParticipant> GetEnumerator() =>
            throw new InvalidOperationException("private enumerable detail");

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
