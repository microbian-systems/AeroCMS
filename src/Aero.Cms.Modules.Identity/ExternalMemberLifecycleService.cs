using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Modules.Identity;

/// <summary>Controls whether the host requires a single coordinating lifecycle participant.</summary>
public sealed class ExternalMemberLifecycleOptions
{
    /// <summary>When true, lifecycle transitions require exactly one server-side participant.</summary>
    public bool RequireParticipant { get; set; } = true;
}

/// <summary>Represents the manager- and site-scoped external-member activity request after boundary validation.</summary>
public sealed record ExternalMemberLifecycleCommand(
    long TenantId,
    long SiteId,
    long ManagerUserId,
    long ExternalMemberId,
    bool IsActive,
    long ExpectedSecurityVersion,
    string? CorrelationId);

/// <summary>Provides immutable facts to a server-only participant before CMS records are staged.</summary>
public sealed record ExternalMemberLifecycleTransition(
    long TenantId,
    long SiteId,
    long ManagerUserId,
    long ExternalMemberId,
    bool CurrentIsActive,
    bool NextIsActive,
    long CurrentSecurityVersion,
    long NextSecurityVersion,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId);

/// <summary>Contains the opaque preparation acknowledgement returned by a lifecycle participant.</summary>
public sealed record ExternalMemberLifecycleParticipantReceipt(string Operation);

/// <summary>
/// Prepares host-owned records using the exact CMS session.
/// Implementations may query, load, fence, and stage records on that session, but must not open another session,
/// save, publish, or perform network I/O.
/// </summary>
public interface IExternalMemberLifecycleParticipant
{
    Task<Result<ExternalMemberLifecycleParticipantReceipt, AeroError>> EnlistAsync(
        IDocumentSession session,
        ExternalMemberLifecycleTransition transition,
        CancellationToken cancellationToken = default);
}

/// <summary>Represents the browser-safe outcome of a lifecycle transition.</summary>
public sealed record ExternalMemberLifecycleResult(
    long ExternalMemberId,
    bool IsActive,
    long SecurityVersion,
    DateTimeOffset? ModifiedOn);

/// <summary>Coordinates a server-side external-member lifecycle transition.</summary>
public interface IExternalMemberLifecycleService
{
    Task<Result<ExternalMemberLifecycleResult, AeroError>> TransitionAsync(
        ExternalMemberLifecycleCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Validates canonical server-side lifecycle input before any session mutation.</summary>
public sealed class ExternalMemberLifecycleCommandValidator : AbstractValidator<ExternalMemberLifecycleCommand>
{
    public ExternalMemberLifecycleCommandValidator()
    {
        RuleFor(value => value.TenantId).GreaterThan(0);
        RuleFor(value => value.SiteId).GreaterThan(0);
        RuleFor(value => value.ManagerUserId).GreaterThan(0);
        RuleFor(value => value.ExternalMemberId).GreaterThan(0);
        RuleFor(value => value.ExpectedSecurityVersion).GreaterThan(0);
        RuleFor(value => value.CorrelationId)
            .MaximumLength(256)
            .Must(value => value is null ||
                (!string.IsNullOrWhiteSpace(value) && value.All(character => !char.IsControl(character))))
            .WithMessage("Correlation identifier is invalid.");
    }
}

/// <summary>Validates browser-supplied lifecycle input at the administration boundary.</summary>
public sealed class ExternalMemberLifecycleRequestValidator : AbstractValidator<ExternalMemberLifecycleRequest>
{
    public ExternalMemberLifecycleRequestValidator()
    {
        RuleFor(value => value.ExpectedSecurityVersion).GreaterThan(0);
        RuleFor(value => value.CorrelationId)
            .MaximumLength(256)
            .Must(value => value is null ||
                (!string.IsNullOrWhiteSpace(value) && value.All(character => !char.IsControl(character))))
            .WithMessage("Correlation identifier is invalid.");
        RuleFor(value => value.AdditionalProperties).Must(value => value is null || value.Count == 0);
    }
}

public sealed class ExternalMemberLifecycleService(
    IDocumentSession session,
    IValidator<ExternalMemberLifecycleCommand> validator,
    IEnumerable<IExternalMemberLifecycleParticipant> participants,
    IOptions<ExternalMemberLifecycleOptions> options,
    TimeProvider timeProvider) : IExternalMemberLifecycleService
{
    public async Task<Result<ExternalMemberLifecycleResult, AeroError>> TransitionAsync(
        ExternalMemberLifecycleCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Prelude.Fail<ExternalMemberLifecycleResult, AeroError>(
                AeroError.ValidationError(validation.Errors.Select(error => error.ErrorMessage)));
        }

        var participantList = participants.ToArray();
        if (participantList.Length > 1 || (options.Value.RequireParticipant && participantList.Length != 1))
            return Unavailable();

        try
        {
            var assignments = await session.Query<ExternalMemberSiteAssignment>()
                .Where(value => value.ExternalMemberId == command.ExternalMemberId &&
                    value.TenantId == command.TenantId && value.SiteId == command.SiteId)
                .ToListAsync(cancellationToken);
            if (assignments.Count != 1)
                return NotFound();

            var member = await session.LoadAsync<ExternalMember>(command.ExternalMemberId, cancellationToken);
            if (member is null)
                return NotFound();

            if (member.SecurityVersion != command.ExpectedSecurityVersion)
                return Conflict();

            if (member.IsActive == command.IsActive)
            {
                return Prelude.Ok<ExternalMemberLifecycleResult, AeroError>(new(
                    member.Id, member.IsActive, member.SecurityVersion, member.ModifiedOn));
            }

            if (member.SecurityVersion == long.MaxValue)
                return Conflict();

            var now = timeProvider.GetUtcNow();
            var nextSecurityVersion = checked(member.SecurityVersion + 1);
            var transition = new ExternalMemberLifecycleTransition(
                command.TenantId,
                command.SiteId,
                command.ManagerUserId,
                command.ExternalMemberId,
                member.IsActive,
                command.IsActive,
                member.SecurityVersion,
                nextSecurityVersion,
                now,
                command.CorrelationId);

            if (participantList.Length == 1)
            {
                Result<ExternalMemberLifecycleParticipantReceipt, AeroError> preparation;
                try
                {
                    preparation = await participantList[0].EnlistAsync(session, transition, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    session.ClearChanges();
                    throw;
                }
                catch
                {
                    session.ClearChanges();
                    return Unavailable();
                }

                if (preparation is Result<ExternalMemberLifecycleParticipantReceipt, AeroError>.Failure(var error))
                {
                    session.ClearChanges();
                    return Prelude.Fail<ExternalMemberLifecycleResult, AeroError>(error);
                }

                if (preparation is not Result<ExternalMemberLifecycleParticipantReceipt, AeroError>.Ok(var receipt) ||
                    receipt is null || string.IsNullOrWhiteSpace(receipt.Operation) ||
                    receipt.Operation.Any(char.IsControl))
                {
                    session.ClearChanges();
                    return Unavailable();
                }
            }

            member.IsActive = command.IsActive;
            member.SecurityVersion = nextSecurityVersion;
            member.ModifiedOn = now;
            member.ModifiedBy = command.ManagerUserId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            session.Store(member);

            if (!command.IsActive)
            {
                var localSessions = await session.Query<ExternalMemberSession>()
                    .Where(value => value.ExternalMemberId == command.ExternalMemberId && value.RevokedAt == null)
                    .ToListAsync(cancellationToken);
                foreach (var localSession in localSessions)
                {
                    localSession.RevokedAt = now;
                    localSession.ModifiedOn = now;
                    localSession.ModifiedBy = member.ModifiedBy;
                    session.Store(localSession);
                }
            }

            await session.SaveChangesAsync(cancellationToken);
            return Prelude.Ok<ExternalMemberLifecycleResult, AeroError>(new(
                member.Id, member.IsActive, member.SecurityVersion, member.ModifiedOn));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            session.ClearChanges();
            throw;
        }
        catch (CommitOutcomeUnknownException)
        {
            throw;
        }
        catch (PostCommitCallbackException)
        {
            throw;
        }
        catch (ConcurrencyException)
        {
            session.ClearChanges();
            return Conflict();
        }
        catch
        {
            session.ClearChanges();
            return Unavailable();
        }
    }

    private static Result<ExternalMemberLifecycleResult, AeroError> Unavailable() =>
        Prelude.Fail<ExternalMemberLifecycleResult, AeroError>(
            AeroError.DatabaseError("External member lifecycle is unavailable."));

    private static Result<ExternalMemberLifecycleResult, AeroError> NotFound() =>
        Prelude.Fail<ExternalMemberLifecycleResult, AeroError>(
            AeroError.NotFoundError("External member is unavailable."));

    private static Result<ExternalMemberLifecycleResult, AeroError> Conflict() =>
        Prelude.Fail<ExternalMemberLifecycleResult, AeroError>(
            AeroError.ConflictError("External member lifecycle conflicts with current state."));
}
