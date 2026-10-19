using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Modules.Identity;

/// <summary>Identifies the server-owned creation path for a newly created external member.</summary>
public enum ExternalMemberCreationKind
{
    LocalCredential = 0,
    FederatedIdentity = 1
}

/// <summary>Represents the immutable server-owned facts of a new external-member creation.</summary>
public sealed record ExternalMemberCreatedTransition(
    long TenantId,
    long SiteId,
    long ExternalMemberId,
    long SecurityVersion,
    ExternalMemberCreationKind Kind,
    DateTimeOffset OccurredAtUtc);

/// <summary>Contains the participant acknowledgement for a new external-member transition.</summary>
public sealed record ExternalMemberCreationParticipantReceipt(
    ExternalMemberCreatedTransition Transition,
    string Operation);

/// <summary>Stages server-owned records on the exact CMS creation session without saving it.</summary>
public interface IExternalMemberCreationParticipant
{
    Task<Result<ExternalMemberCreationParticipantReceipt, AeroError>> EnlistAsync(
        IDocumentSession session,
        ExternalMemberCreatedTransition transition,
        CancellationToken cancellationToken = default);
}

/// <summary>Controls whether a host requires one authority-creation participant for new members.</summary>
public sealed class ExternalMemberCreationCoordinationOptions
{
    /// <summary>Requires exactly one participant when a new member is created.</summary>
    public bool RequireParticipant { get; set; }
}

/// <summary>Validates the server-owned facts before any creation participant is enlisted.</summary>
public sealed class ExternalMemberCreatedTransitionValidator : AbstractValidator<ExternalMemberCreatedTransition>
{
    public ExternalMemberCreatedTransitionValidator()
    {
        RuleFor(value => value.TenantId).GreaterThan(0);
        RuleFor(value => value.SiteId).GreaterThan(0);
        RuleFor(value => value.ExternalMemberId).GreaterThan(0);
        RuleFor(value => value.SecurityVersion).Equal(1);
        RuleFor(value => value.Kind).IsInEnum();
        RuleFor(value => value.OccurredAtUtc)
            .Must(value => value != default && value.Offset == TimeSpan.Zero)
            .WithMessage("Creation occurrence must be a non-default UTC instant.");
    }
}

/// <summary>Coordinates the server-owned new-member transition before the caller's sole save.</summary>
public sealed class ExternalMemberCreationCoordinator(
    IEnumerable<IExternalMemberCreationParticipant> participants,
    IOptions<ExternalMemberCreationCoordinationOptions> options)
{
    /// <summary>Enlists authority creation on the supplied CMS session.</summary>
    public Task<Result<ExternalMemberCreationParticipantReceipt, AeroError>> EnlistAsync(
        IDocumentSession session,
        ExternalMemberCreatedTransition transition,
        CancellationToken cancellationToken = default) => EnlistCoreAsync(session, transition, cancellationToken);

    private async Task<Result<ExternalMemberCreationParticipantReceipt, AeroError>> EnlistCoreAsync(
        IDocumentSession session,
        ExternalMemberCreatedTransition transition,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (session is null || !IsCanonicalTransition(transition) || participants is null || options is null)
                return Unavailable();

            var participantList = participants.ToArray();
            var requireParticipant = options.Value.RequireParticipant;
            if (participantList.Any(participant => participant is null) || participantList.Length > 1 ||
                (requireParticipant && participantList.Length != 1))
                return Unavailable();

            if (participantList.Length == 0)
                return Prelude.Ok<ExternalMemberCreationParticipantReceipt, AeroError>(new(transition, "standalone"));

            var enlisted = await participantList[0].EnlistAsync(session, transition, cancellationToken);
            if (enlisted is not Result<ExternalMemberCreationParticipantReceipt, AeroError>.Ok(var receipt) ||
                !IsMatchingReceipt(receipt, transition))
                return Unavailable();

            return Prelude.Ok<ExternalMemberCreationParticipantReceipt, AeroError>(receipt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Unavailable();
        }
    }

    private static bool IsCanonicalTransition(ExternalMemberCreatedTransition? transition) =>
        transition is not null && new ExternalMemberCreatedTransitionValidator().Validate(transition).IsValid;

    private static bool IsMatchingReceipt(
        ExternalMemberCreationParticipantReceipt? receipt,
        ExternalMemberCreatedTransition transition) =>
        receipt is not null &&
        receipt.Transition is not null &&
        IsCanonicalTransition(receipt.Transition) &&
        receipt.Transition == transition &&
        !string.IsNullOrWhiteSpace(receipt.Operation) &&
        !receipt.Operation.Any(char.IsControl);

    private static Result<ExternalMemberCreationParticipantReceipt, AeroError> Unavailable() =>
        Prelude.Fail<ExternalMemberCreationParticipantReceipt, AeroError>(
            AeroError.DatabaseError("External-member creation coordination is unavailable."));
}
