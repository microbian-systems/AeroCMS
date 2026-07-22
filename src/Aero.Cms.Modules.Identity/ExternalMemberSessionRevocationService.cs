using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Identity;

/// <summary>Identifies one local external-member session within its host-resolved site boundary.</summary>
public sealed record ExternalMemberSessionRevocationRequest(
    long TenantId,
    long SiteId,
    long ExternalMemberId,
    long ExternalMemberSessionId,
    string Provider,
    long SecurityVersion);

/// <summary>Contains non-cookie data retained only long enough to attempt an upstream logout.</summary>
public sealed record ExternalMemberSessionRevocationReceipt(
    long TenantId,
    long SiteId,
    string Provider,
    string? ProviderSessionReference);

/// <summary>Revokes owned local member sessions through one fail-closed persistence path.</summary>
public interface IExternalMemberSessionRevocationService
{
    Task<Result<ExternalMemberSessionRevocationReceipt, AeroError>> RevokeAsync(
        ExternalMemberSessionRevocationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Validates site membership and session ownership before persisting a local revocation.</summary>
public sealed class ExternalMemberSessionRevocationService(
    IDocumentSession session,
    TimeProvider timeProvider) : IExternalMemberSessionRevocationService
{
    public async Task<Result<ExternalMemberSessionRevocationReceipt, AeroError>> RevokeAsync(
        ExternalMemberSessionRevocationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsCanonical(request))
        {
            return Denied();
        }

        try
        {
            var localSession = await session.LoadAsync<ExternalMemberSession>(
                request.ExternalMemberSessionId, cancellationToken);
            if (localSession is null ||
                localSession.TenantId != request.TenantId ||
                localSession.SiteId != request.SiteId ||
                localSession.ExternalMemberId != request.ExternalMemberId ||
                localSession.TenantId <= 0 || localSession.SiteId <= 0 ||
                localSession.SecurityVersion != request.SecurityVersion ||
                !string.Equals(localSession.AuthenticationProvider, request.Provider, StringComparison.Ordinal))
            {
                return Denied();
            }

            var now = timeProvider.GetUtcNow();
            var newlyRevoked = localSession.RevokedAt is null;
            if (newlyRevoked)
            {
                localSession.RevokedAt = now;
                localSession.ModifiedOn = now;
                session.Store(localSession);
                await session.SaveChangesAsync(cancellationToken);
            }

            return Prelude.Ok<ExternalMemberSessionRevocationReceipt, AeroError>(new(
                localSession.TenantId,
                localSession.SiteId,
                request.Provider,
                newlyRevoked ? localSession.ProviderSessionReference : null));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            session.ClearChanges();
            return Prelude.Fail<ExternalMemberSessionRevocationReceipt, AeroError>(
                AeroError.DatabaseError("The local member session could not be revoked."));
        }
    }

    private static bool IsCanonical(ExternalMemberSessionRevocationRequest request) =>
        request.TenantId > 0 && request.SiteId > 0 && request.ExternalMemberId > 0 &&
        request.ExternalMemberSessionId > 0 &&
        request.SecurityVersion > 0 &&
        ExternalMemberSessionProviders.IsSupported(request.Provider);

    private static Result<ExternalMemberSessionRevocationReceipt, AeroError> Denied() =>
        Prelude.Fail<ExternalMemberSessionRevocationReceipt, AeroError>(
            AeroError.NotFoundError("The local member session is unavailable."));
}
